namespace FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Application authority for the customer sync query.
///
/// The handler depends only on the inward, provider-independent capability port
/// (<c>ISyncablePort&lt;SourceCustomer&gt;</c>), the provider-independent ownership oracle
/// (<c>IAccountingSourceOwnership</c>), and the trusted Tenant context — never on a concrete
/// provider, and never on <c>ISender</c> itself. It is dispatchable through <c>ISender</c>,
/// satisfying the CQRS direction: <c>delivery adapter → ISender → query → handler → capability port</c>.
///
/// The handler drives EXACTLY ONE bounded page per request and returns it to the caller;
/// pagination is caller-controlled via <c>SyncBatch{TRecord}.NextCursor</c>. It never loops
/// through pages or accumulates a full collection.
/// </summary>
public sealed class SynchronizeCustomersHandler
    : IRequestHandler<SynchronizeCustomersQuery, SyncBatch<SourceCustomer>>
{
    private readonly ISyncablePort<SourceCustomer> _customers;
    private readonly IAccountingSourceOwnership _ownership;

    public SynchronizeCustomersHandler(
        ISyncablePort<SourceCustomer> customers,
        IAccountingSourceOwnership ownership)
    {
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
    }

    public async Task<SyncBatch<SourceCustomer>> Handle(
        SynchronizeCustomersQuery command,
        CancellationToken cancellationToken)
    {
        // 1. Tenant authority must originate from a trusted execution context, never from the
        //    request. This is the Application use case enforcing the tenant-authority invariant.
        command.Tenant.AssertTrusted();

        // 2. Fail-closed ownership check: proving the trusted Tenant owns the requested source
        //    is the orchestration layer's responsibility, never the adapter's. Any failure
        //    (unknown tenant/source, lookup failure) is treated as NOT owned, so no provider
        //    work proceeds. Cancellation during the check propagates.
        var owned = await _ownership
            .IsOwnedByAsync(command.Tenant.TenantId, command.Request.SourceId, cancellationToken)
            .ConfigureAwait(false);

        if (!owned)
        {
            throw new UnauthorizedSourceException(
                command.Request.SourceId,
                "The trusted Tenant does not own the requested AccountingSource.");
        }

        // 3. Drive exactly ONE bounded page. The caller continues via SyncBatch.NextCursor.
        SyncBatch<SourceCustomer> page = await _customers
            .SyncAsync(command.Request, cancellationToken)
            .ConfigureAwait(false);

        // 4. Application boundary enforces the batch-size limit: a provider must never return a
        //    page larger than the explicitly requested batch size. Detection here, not silence.
        if (command.Request.BatchSize is { } requested
            && page.Records.Count > requested)
        {
            throw new SyncProviderException(
                SyncFailureCategory.Permanent,
                $"Capability returned {page.Records.Count} records, exceeding the requested batch size of {requested}.");
        }

        return page;
    }
}

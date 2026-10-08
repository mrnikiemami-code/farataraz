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
/// (<c>ISyncablePort&lt;SourceCustomer&gt;</c>) and the trusted Tenant context — never on a
/// concrete provider, and never on <c>ISender</c> itself. It first asserts the Tenant context
/// is established by a trusted execution context (never client/LLM/MCP input), then drives the
/// bounded-page cursor contract to completion, which is the meaningful W1 behavior (resume from
/// the stored cursor, never restart). It is dispatchable through <c>ISender</c>, satisfying the
/// CQRS direction: <c>delivery adapter → ISender → query → handler → capability port</c>.
/// </summary>
public sealed class SynchronizeCustomersHandler
    : IRequestHandler<SynchronizeCustomersQuery, IReadOnlyList<SourceCustomer>>
{
    private readonly ISyncablePort<SourceCustomer> _customers;

    public SynchronizeCustomersHandler(ISyncablePort<SourceCustomer> customers)
        => _customers = customers;

    public async Task<IReadOnlyList<SourceCustomer>> Handle(
        SynchronizeCustomersQuery command,
        CancellationToken cancellationToken)
    {
        // Tenant authority must originate from a trusted execution context, never from the
        // request. This is the Application use case enforcing the tenant invariant.
        command.Tenant.AssertTrusted();

        var collected = new List<SourceCustomer>();
        SyncRequest request = command.Request;
        SyncCursor? cursor = null;

        do
        {
            SyncBatch<SourceCustomer> batch = await _customers
                .SyncAsync(request, cancellationToken)
                .ConfigureAwait(false);

            collected.AddRange(batch.Records);
            cursor = batch.NextCursor;

            // Resume from the returned cursor, never restart. Preserve the source, mode,
            // and batch size; only the cursor advances.
            if (cursor is not null)
            {
                request = new SyncRequest(
                    command.Request.SourceId,
                    command.Request.Mode,
                    cursor,
                    command.Request.BatchSize);
            }
        }
        while (cursor is not null);

        return collected;
    }
}

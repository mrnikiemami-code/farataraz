namespace FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Authorization;
using FaraTaraz.Modules.AccountingSources.Capabilities;
using FaraTaraz.Modules.Ingestion.Application.Ports;
using FaraTaraz.Modules.Ingestion.Contracts.SourceModel;
using FaraTaraz.Modules.Ingestion.Contracts.Synchronization;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using MediatR;

/// <summary>
/// Application authority for the customer ingestion use case (W3 M3/M4).
///
/// <b>Admission first, then one atomic unit of work.</b> Order is fixed:
/// <c>AssertTrusted</c> → tenant/scope agreement → <c>IsOwnedByAsync</c> (must be TRUE) →
/// checkpoint read → one bounded provider page → transactional write (records + checkpoint + run).
/// It depends only on inward Domain/AccountingSources contracts and the M1 persistence ports —
/// never on Infrastructure, EF, Npgsql or concrete repositories.
///
/// It reuses the W1 bounded-page contract (<see cref="ISyncablePort{TRecord}.SyncAsync"/>) and the
/// W2 persistence foundation through the write port, whose
/// <see cref="IIngestionWriteStore.ExecuteInTransactionAsync{TResult}"/> is the ONLY commit
/// boundary: a checkpoint can never advance beyond committed source records, the ORIGINAL observed
/// version is passed (no silent stale retry), and any failure/changed record/cancellation before
/// commit rolls the whole step back. It is NOT a delivery surface, worker or scheduler.
/// </summary>
public sealed class RunCustomerSyncHandler : IRequestHandler<RunCustomerSyncCommand, IngestionRunResult>
{
    private readonly IIngestionWriteStore _writes;
    private readonly IIngestionCheckpointStore _checkpoints;
    private readonly IIngestionTrustedTenantScope _trustedTenant;
    private readonly IAccountingSourceOwnership _ownership;
    private readonly IAccountingSourceProviderResolver _providerResolver;
    private readonly ISyncablePort<SourceCustomer> _customers;

    /// <summary>Wraps the persistence ports, the ownership + provider oracles and the customer capability port.</summary>
    public RunCustomerSyncHandler(
        IIngestionWriteStore writes,
        IIngestionCheckpointStore checkpoints,
        IIngestionTrustedTenantScope trustedTenant,
        IAccountingSourceOwnership ownership,
        IAccountingSourceProviderResolver providerResolver,
        ISyncablePort<SourceCustomer> customers)
    {
        _writes = writes ?? throw new ArgumentNullException(nameof(writes));
        _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        _trustedTenant = trustedTenant ?? throw new ArgumentNullException(nameof(trustedTenant));
        _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
        _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
    }

    /// <inheritdoc />
    public async Task<IngestionRunResult> Handle(RunCustomerSyncCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Tenant authority from the trusted execution context only.
        command.Tenant.AssertTrusted();

        // 2. The trusted context must agree with the tenant the adapter will persist under; a
        //    disagreement would admit ownership for one tenant while writing under another.
        var boundTenant = _trustedTenant.TrustedTenantId;
        if (boundTenant is null ||
            !string.Equals(boundTenant.Value.Value, command.Tenant.TenantId.Value, StringComparison.Ordinal))
        {
            throw new UnauthorizedTenantException(
                "The trusted tenant context does not match the database tenant scope.");
        }

        // 3. Fail-closed ownership admission BEFORE any provider call or write.
        var owned = await _ownership
            .IsOwnedByAsync(command.Tenant.TenantId, command.Request.SourceId, cancellationToken)
            .ConfigureAwait(false);

        if (!owned)
        {
            throw new UnauthorizedSourceException(
                command.Request.SourceId,
                "The trusted Tenant does not own the requested AccountingSource.");
        }

        // 4. Resolve the AUTHORITATIVE provider identity from the source's persisted configuration.
        //    The provider is never taken from the request; a missing source or blank/absent provider
        //    configuration fails closed before any provider call or write (provenance authority).
        var resolvedProvider = await _providerResolver
            .ResolveProviderAsync(command.Tenant.TenantId, command.Request.SourceId, cancellationToken)
            .ConfigureAwait(false);

        if (resolvedProvider is not { } provider || string.IsNullOrWhiteSpace(provider.Value))
        {
            throw new UnauthorizedSourceException(
                command.Request.SourceId,
                "The AccountingSource has no resolvable provider configuration.");
        }

        var capabilityName = _customers.CapabilityName;

        // 5. Observe the persisted checkpoint (resume point + CAS guard) and drive ONE bounded page.
        // The restart/cursor/terminality decisions are pure Domain policy (IngestionSyncPolicy);
        // this method keeps the I/O order: restart validity → checkpoint read → continuation rule.
        var isExplicitRestart = command.Intent == CustomerSyncIntent.Restart;

        IngestionSyncPolicy.EnsureExplicitRestartHasNoCursor(isExplicitRestart, command.Request.Cursor);

        var observed = await _checkpoints
            .ReadAsync(command.Request.SourceId, capabilityName, cancellationToken)
            .ConfigureAwait(false);

        // A completed synchronization is TERMINAL. A normal step must reject it before any
        // provider call or write; only an explicit restart may pass it, and that restart is
        // transitioned through the observed-version CAS below (the marker is never deleted and
        // its version is never reset).
        IngestionSyncPolicy.EnsureContinuationAllowed(
            isExplicitRestart, observed?.State, command.Request.SourceId);

        IngestionCheckpointRef? resumable = null;

        if (observed is { State: SyncCheckpointState.Active } && !isExplicitRestart)
        {
            resumable = observed;
        }

        var effectiveRequest = ResolveResumeRequest(command.Request, resumable, capabilityName);

        var page = await _customers.SyncAsync(effectiveRequest, cancellationToken).ConfigureAwait(false);

        if (command.Request.BatchSize is { } requested && page.Records.Count > requested)
        {
            throw new SyncProviderException(
                SyncFailureCategory.Permanent,
                $"Capability returned {page.Records.Count} records, exceeding the requested batch size of {requested}.");
        }

        // Page consistency: a terminal page must carry no continuation cursor and a non-terminal
        // page must carry one. SyncBatch enforces this by construction; validate defensively so a
        // future contract change cannot silently persist an inconsistent completion.
        if (page.IsComplete != (page.NextCursor is null))
        {
            throw new SyncProviderException(
                SyncFailureCategory.Permanent,
                "Capability returned an inconsistent page: IsComplete and NextCursor disagree.");
        }

        // 5. Persist records + checkpoint + run state atomically through the write port.
        return await _writes.ExecuteInTransactionAsync(async (session, innerCt) =>
        {
            var runId = await session
                .StartRunAsync(command.Request.SourceId, capabilityName, command.Request.Mode, innerCt)
                .ConfigureAwait(false);

            long created = 0;
            foreach (var record in page.Records)
            {
                var probe = await session.ProbeRecordAsync(record.RecordId, innerCt).ConfigureAwait(false);

                if (probe is not null)
                {
                    // Identity exists: the changed-vs-idempotent decision is pure Domain policy.
                    // A stored null fingerprint is never treated as absent, and null == null is an
                    // idempotent duplicate.
                    IngestionSyncPolicy.EnsureUnchangedOrIdempotent(
                        probe.ContentFingerprint,
                        record.Version?.ContentFingerprint,
                        record.RecordId.ExternalId);

                    continue; // identical duplicate (including null == null): idempotent
                }

                var provenance = new SourceProvenance(
                    record.RecordId, provider, command.Request.SourceId, DateTime.UtcNow);

                if (await session
                        .InsertRecordAsync(record.RecordId, record.Version, provenance, innerCt)
                        .ConfigureAwait(false))
                {
                    created++;
                }
            }

            // Checkpoint state is driven by the VALIDATED completion flag — not by cursor
            // nullability alone.
            if (!page.IsComplete)
            {
                // Continuation: resumable. Pass the ORIGINAL observed version (CAS).
                await session
                    .SaveCheckpointAsync(
                        command.Request.SourceId, capabilityName, page.NextCursor!.Token,
                        DateTime.UtcNow, observed?.Version, innerCt, SyncCheckpointState.Active)
                    .ConfigureAwait(false);
            }
            else
            {
                // Durable terminal completion: atomically mark the checkpoint non-resumable with the
                // ORIGINAL observed version (CAS). Completion of the SYNC is distinct from the run
                // step's own Completed outcome.
                await session
                    .SaveCheckpointAsync(
                        command.Request.SourceId, capabilityName,
                        effectiveRequest.Cursor?.Token ?? string.Empty,
                        DateTime.UtcNow, observed?.Version, innerCt, SyncCheckpointState.Completed)
                    .ConfigureAwait(false);
            }

            await session
                .FinishRunAsync(runId, IngestionRunOutcome.Completed, created, null, innerCt)
                .ConfigureAwait(false);

            return new IngestionRunResult(
                runId, page.Records.Count, created, page.IsComplete, page.NextCursor?.Token);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Restores the persisted continuation cursor and rejects conflicts/malformed state. Never
    /// silently restarts and never skips a page.
    /// </summary>
    private static SyncRequest ResolveResumeRequest(
        SyncRequest request, IngestionCheckpointRef? persisted, string capabilityName)
    {
        if (request.Cursor is not null)
        {
            SyncCursors.ValidateResume(request, capabilityName);
        }

        if (persisted is null)
        {
            return request;
        }

        // Domain policy (C8): the caller-supplied cursor, if any, must agree with the persisted one.
        return IngestionSyncPolicy.ResumeFromPersistedCursor(request, persisted.CursorToken, capabilityName);
    }
}

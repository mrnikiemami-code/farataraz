namespace FaraTaraz.Modules.Ingestion.Infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Authorization;
using FaraTaraz.Modules.AccountingSources.Capabilities;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

/// <summary>
/// First bounded W3 ingestion-engine orchestration slice (customer capability only).
///
/// <b>Ownership admission first, then one atomic unit of work.</b> Before any provider call or
/// database write it (1) requires a genuinely trusted <see cref="TenantContext"/>
/// (<see cref="TenantContext.AssertTrusted"/> — a missing/client-origin context throws) and
/// (2) validates source ownership through the provider-independent
/// <see cref="IAccountingSourceOwnership.IsOwnedByAsync"/>, which is fail-closed (unknown source,
/// cross-tenant, or lookup failure is never authorization success). A caller-supplied tenant id is
/// never authority: tenant scope comes only from the trusted context / trusted
/// <see cref="DatabaseTenantScope"/>.
///
/// It reuses the W1 bounded-page sync contract (<see cref="ISyncablePort{TRecord}.SyncAsync" />,
/// one page per call) and the W2 persistence foundation: run state, source-record identity and
/// checkpoint progression are written through the existing repositories inside a single
/// <see cref="IngestionUnitOfWork"/>, so the checkpoint can never advance beyond committed source
/// records. Checkpoint CAS (<c>observedVersion</c>) is preserved — a stale writer is rejected and
/// nothing is retried; any failure, unsupported changed record, or cancellation before commit rolls
/// the whole step back.
///
/// This type lives in the module Infrastructure layer because that is the approved write boundary
/// (<c>OwnershipPathAuthorizationGuard</c>); it contains no EF queries of its own and adds no
/// duplicated tenant/ownership decision — it only composes the module's inward ports and its
/// repositories. It is NOT a delivery surface, worker, scheduler or background job.
/// </summary>
public sealed class IngestionRunCoordinator
{
    private readonly IngestionUnitOfWork _unitOfWork;
    private readonly SourceRecordRepository _records;
    private readonly SyncCheckpointRepository _checkpoints;
    private readonly SyncRunRepository _runs;
    private readonly IAccountingSourceOwnership _ownership;

    /// <summary>Wraps the atomic unit of work, the tenant-scoped repositories and the ownership port.</summary>
    public IngestionRunCoordinator(
        IngestionUnitOfWork unitOfWork,
        SourceRecordRepository records,
        SyncCheckpointRepository checkpoints,
        SyncRunRepository runs,
        IAccountingSourceOwnership ownership)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
    }

    /// <summary>
    /// Runs exactly one bounded synchronization step for one owned source over a trusted tenant:
    /// admission → one page → atomic persistence of records + checkpoint + run state.
    /// </summary>
    public async Task<IngestionRunResult> ExecuteAsync(
        TenantContext tenant,
        SyncRequest request,
        ISyncablePort<SourceCustomer> capability,
        ProviderId provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capability);

        // 1. Tenant authority from the trusted execution context only.
        tenant.AssertTrusted();

        // 2. Fail-closed ownership admission BEFORE any provider call or write.
        var owned = await _ownership
            .IsOwnedByAsync(tenant.TenantId, request.SourceId, cancellationToken)
            .ConfigureAwait(false);

        if (!owned)
        {
            throw new UnauthorizedSourceException(
                request.SourceId,
                "The trusted Tenant does not own the requested AccountingSource.");
        }

        var capabilityName = capability.CapabilityName;

        // 3. Observe the persisted checkpoint (resume point + CAS guard) and drive ONE bounded page.
        var observed = await _checkpoints
            .GetAsync(tenant.TenantId, request.SourceId, capabilityName, cancellationToken)
            .ConfigureAwait(false);

        var effectiveRequest = ResolveResumeRequest(request, observed, capabilityName);

        var page = await capability.SyncAsync(effectiveRequest, cancellationToken).ConfigureAwait(false);

        if (request.BatchSize is { } requested && page.Records.Count > requested)
        {
            throw new SyncProviderException(
                SyncFailureCategory.Permanent,
                $"Capability returned {page.Records.Count} records, exceeding the requested batch size of {requested}.");
        }

        // 4. Persist records + checkpoint + run state atomically. Commit only if every write
        //    succeeds; otherwise (failure, stale CAS, unsupported changed record, cancellation)
        //    the unit of work rolls the whole step back.
        IngestionRunResult result = null!;
        await _unitOfWork.ExecuteAsync(async innerCt =>
        {
            var run = await _runs
                .StartAsync(tenant.TenantId, request.SourceId, capabilityName, request.Mode.ToString(), innerCt)
                .ConfigureAwait(false);

            long created = 0;
            foreach (var record in page.Records)
            {
                var probe = await _records
                    .ProbeAsync(tenant.TenantId, record.RecordId, innerCt)
                    .ConfigureAwait(false);

                if (probe is not null)
                {
                    // The identity EXISTS. Classify by the fingerprint VALUE (which may itself be
                    // null) — a stored null fingerprint is never treated as "absent".
                    var incomingFingerprint = record.Version?.ContentFingerprint;
                    if (!string.Equals(probe.ContentFingerprint, incomingFingerprint, StringComparison.Ordinal))
                    {
                        // Changed / ambiguous content: not supported yet (FT-DATA-003). Fail closed,
                        // never silently discard the update.
                        throw new UnsupportedChangedRecordException(record.RecordId.ExternalId);
                    }

                    continue; // identical duplicate (including null == null): idempotent, first delivery canonical
                }

                var provenance = new SourceProvenance(
                    record.RecordId, provider, request.SourceId, DateTime.UtcNow);

                if (await _records
                        .InsertOrUpdateAsync(tenant.TenantId, record.RecordId, record.Version, provenance, innerCt)
                        .ConfigureAwait(false))
                {
                    created++;
                }
            }

            if (page.NextCursor is { } next)
            {
                // No early cursor advancement: the checkpoint moves only inside this transaction,
                // guarded by the observed version (no silent stale-writer retry).
                await _checkpoints
                    .SaveAsync(
                        tenant.TenantId, request.SourceId, capabilityName, next.Token,
                        DateTime.UtcNow, observed?.Version, innerCt)
                    .ConfigureAwait(false);
            }

            // Terminal page (IsComplete == true, NextCursor == null): the previous continuation
            // checkpoint is RETAINED (no write, no deletion) — deleting/clearing a checkpoint is
            // NOT an approved behavior (see RECOVERY FT-W3-R2: architect decision requested).

            await _runs
                .FinishAsync(run.Id, "Completed", created, null, innerCt)
                .ConfigureAwait(false);

            result = new IngestionRunResult(
                run.Id, page.Records.Count, created, page.IsComplete, page.NextCursor?.Token);
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Resolves the request the capability is called with, restoring the persisted continuation
    /// cursor and rejecting conflicts/malformed state. It never silently restarts from the
    /// beginning and never skips a page:
    /// <list type="bullet">
    /// <item>a caller-supplied cursor is validated against the W1 scope contract first;</item>
    /// <item>with no persisted checkpoint the caller's (possibly absent) cursor is the first page;</item>
    /// <item>with a persisted checkpoint the persisted cursor is restored, and a caller cursor that
    /// conflicts with it is rejected (the opaque token never leaks into messages).</item>
    /// </list>
    /// </summary>
    private static SyncRequest ResolveResumeRequest(
        SyncRequest request, SyncCheckpointEntity? persisted, string capabilityName)
    {
        if (request.Cursor is not null)
        {
            SyncCursors.ValidateResume(request, capabilityName);
        }

        if (persisted is null)
        {
            return request;
        }

        // The SyncCursor constructor rejects an empty/oversized token, so a malformed persisted
        // checkpoint fails closed instead of silently restarting the synchronization.
        var persistedCursor = new SyncCursor(
            persisted.CursorToken, new SyncCursorScope(request.SourceId, capabilityName));

        if (request.Cursor is not null &&
            !string.Equals(request.Cursor.Token, persistedCursor.Token, StringComparison.Ordinal))
        {
            throw new InvalidSyncCursorException(
                "Caller-supplied cursor conflicts with the persisted checkpoint; refusing to resume.");
        }

        return new SyncRequest(request.SourceId, request.Mode, persistedCursor, request.BatchSize);
    }
}

/// <summary>Outcome of one bounded ingestion step.</summary>
/// <param name="RunId">The persisted synchronization run id.</param>
/// <param name="RecordsReceived">Records returned by the capability for this page.</param>
/// <param name="RecordsCreated">New source records actually stored (duplicates excluded).</param>
/// <param name="IsComplete">True when the page completed the synchronization.</param>
/// <param name="NextCursorToken">Continuation cursor token, or null when complete.</param>
public sealed record IngestionRunResult(
    long RunId,
    int RecordsReceived,
    long RecordsCreated,
    bool IsComplete,
    string? NextCursorToken);

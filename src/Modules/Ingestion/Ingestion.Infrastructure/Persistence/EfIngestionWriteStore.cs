namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Application.Ports;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Infrastructure adapter implementing the Application-owned ingestion persistence ports
/// (<see cref="IIngestionWriteStore"/> + <see cref="IIngestionCheckpointStore"/>) by mapping to
/// the existing W2 repositories and <see cref="IngestionUnitOfWork"/> (task M2).
///
/// It is a <b>mechanism</b>, not a policy: it never decides ownership or business behavior. All
/// session operations execute inside ONE transaction owned by
/// <see cref="IIngestionWriteStore.ExecuteInTransactionAsync{TResult}"/> (backed by
/// <see cref="IngestionUnitOfWork"/>), so run state, source records and checkpoint movement can
/// never be split into independent commits. Tenant authority comes exclusively from the trusted
/// <see cref="DatabaseTenantScope"/>; the port surface carries no caller-supplied tenant id.
/// </summary>
public sealed class EfIngestionWriteStore : IIngestionWriteStore, IIngestionCheckpointStore, IIngestionTrustedTenantScope
{
    private readonly IngestionUnitOfWork _unitOfWork;
    private readonly SourceRecordRepository _records;
    private readonly SyncCheckpointRepository _checkpoints;
    private readonly SyncRunRepository _runs;
    private readonly DatabaseTenantScope _scope;

    /// <summary>Wraps the unit of work, the tenant-scoped repositories and the trusted tenant scope.</summary>
    public EfIngestionWriteStore(
        IngestionUnitOfWork unitOfWork,
        SourceRecordRepository records,
        SyncCheckpointRepository checkpoints,
        SyncRunRepository runs,
        DatabaseTenantScope scope)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <inheritdoc />
    public async Task<IngestionCheckpointRef?> ReadAsync(
        AccountingSourceId sourceId,
        string capability,
        CancellationToken cancellationToken = default)
    {
        var tenant = RequireTrustedTenant();
        var row = await _checkpoints
            .GetAsync(tenant, sourceId, capability, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new IngestionCheckpointRef(
                row.CursorToken,
                row.Version,
                row.State == SyncCheckpointStates.Completed ? SyncCheckpointState.Completed : SyncCheckpointState.Active);
    }

    /// <inheritdoc />
    public TenantId? TrustedTenantId => _scope.TenantId;

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IIngestionWriteSession, CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        // Fail closed before opening the transaction when no trusted tenant scope is bound.
        var tenant = RequireTrustedTenant();
        var session = new Session(this, tenant);

        try
        {
            TResult result = default!;
            await _unitOfWork.ExecuteAsync(async innerCt =>
            {
                result = await work(session, innerCt).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }
        finally
        {
            // The session is unusable once its transaction has ENDED. Mark it completed on EVERY
            // exit — success, callback exception, persistence failure, rollback, stale CAS or
            // cancellation — so a captured session can never be reused (M2 session-lifetime fix).
            session.MarkCompleted();
        }
    }

    private TenantId RequireTrustedTenant()
        => _scope.TenantId
           ?? throw new UnauthorizedTenantException("No trusted tenant scope is bound for this unit of work.");

    /// <summary>
    /// Transaction-scoped session. It holds the trusted tenant resolved once at the transaction
    /// boundary and refuses every operation once the transaction has completed, so a session can
    /// never be reused outside its <c>ExecuteInTransactionAsync</c> call.
    /// </summary>
    private sealed class Session : IIngestionWriteSession
    {
        private readonly EfIngestionWriteStore _store;
        private readonly TenantId _tenant;
        private bool _completed;

        public Session(EfIngestionWriteStore store, TenantId tenant)
        {
            _store = store;
            _tenant = tenant;
        }

        public void MarkCompleted() => _completed = true;

        private void EnsureActive()
        {
            if (_completed)
            {
                throw new InvalidOperationException(
                    "The ingestion write session can no longer be used after its transaction completed.");
            }
        }

        public async Task<long> StartRunAsync(
            AccountingSourceId sourceId,
            string capability,
            SyncMode mode,
            CancellationToken cancellationToken = default)
        {
            EnsureActive();
            var run = await _store._runs
                .StartAsync(_tenant, sourceId, capability, mode.ToString(), cancellationToken)
                .ConfigureAwait(false);
            return run.Id;
        }

        public async Task<IngestionRecordProbe?> ProbeRecordAsync(
            SourceRecordId recordId,
            CancellationToken cancellationToken = default)
        {
            EnsureActive();
            var probe = await _store._records
                .ProbeAsync(_tenant, recordId, cancellationToken)
                .ConfigureAwait(false);
            return probe is null ? null : new IngestionRecordProbe(probe.ContentFingerprint);
        }

        public async Task<bool> InsertRecordAsync(
            SourceRecordId recordId,
            SourceRecordVersion? version,
            SourceProvenance provenance,
            CancellationToken cancellationToken = default)
        {
            EnsureActive();
            return await _store._records
                .InsertOrUpdateAsync(_tenant, recordId, version, provenance, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task SaveCheckpointAsync(
            AccountingSourceId sourceId,
            string capability,
            string cursorToken,
            DateTime updatedAtUtc,
            long? observedVersion,
            CancellationToken cancellationToken = default,
            SyncCheckpointState state = SyncCheckpointState.Active)
        {
            EnsureActive();
            await _store._checkpoints
                .SaveAsync(_tenant, sourceId, capability, cursorToken, updatedAtUtc, observedVersion, cancellationToken, state)
                .ConfigureAwait(false);
        }

        public async Task FinishRunAsync(
            long runId,
            IngestionRunOutcome outcome,
            long recordsProcessed,
            string? error,
            CancellationToken cancellationToken = default)
        {
            EnsureActive();
            await _store._runs
                .FinishAsync(runId, MapOutcome(outcome), recordsProcessed, error, cancellationToken)
                .ConfigureAwait(false);
        }

        private static string MapOutcome(IngestionRunOutcome outcome) => outcome switch
        {
            IngestionRunOutcome.Completed => SyncRunState.Completed,
            IngestionRunOutcome.Failed => SyncRunState.Failed,
            IngestionRunOutcome.Cancelled => SyncRunState.Cancelled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "Unknown ingestion run outcome.")
        };
    }
}

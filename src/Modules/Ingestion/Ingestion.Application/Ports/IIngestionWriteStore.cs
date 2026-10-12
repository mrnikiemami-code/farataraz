namespace FaraTaraz.Modules.Ingestion.Application.Ports;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.Ingestion.Contracts.SourceModel;
using FaraTaraz.Modules.Ingestion.Contracts.Synchronization;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Application-owned, <b>transaction-scoped</b> persistence boundary for one synchronization step
/// (W3 architecture refactor, task M1).
///
/// The unit of work is the ONLY commit boundary: <see cref="ExecuteInTransactionAsync{TResult}"/>
/// owns the
/// database transaction (implemented in Infrastructure over the existing
/// <c>IngestionUnitOfWork</c>) and commits only after <c>work</c> completes. The
/// <see cref="IIngestionWriteSession"/> operations are scoped to that single transaction and
/// <b>never commit on their own</b>, so run state, source records and checkpoint movement can
/// never be split into independent commits (ADR-010 decision 8; FT-DATA-002).
///
/// This port deliberately exposes no tenant argument: the trusted tenant is resolved from the
/// trusted execution context by the Infrastructure adapter, never from a caller-supplied id, so a
/// caller can never make a tenant id an authorization authority (Constitution A.4/A.5).
///
/// No EF Core, DbContext, Npgsql, database entity or concrete repository type appears here.
/// </summary>
public interface IIngestionWriteStore
{
    /// <summary>
    /// Runs <paramref name="work"/> inside one database transaction and commits only when it
    /// completes. Any exception (including a stale-checkpoint CAS rejection) or cancellation before
    /// the commit rolls the whole unit of work back.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IIngestionWriteSession, CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The operations available inside a single
/// <see cref="IIngestionWriteStore.ExecuteInTransactionAsync{TResult}"/> transaction. Every
/// operation is transaction-scoped: there is no commit, flush or
/// save-changes here, so a session can never accidentally commit on its own.
/// </summary>
public interface IIngestionWriteSession
{
    /// <summary>Starts a synchronization run in the running state and returns its identifier.</summary>
    Task<long> StartRunAsync(
        AccountingSourceId sourceId,
        string capability,
        SyncMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Probes a source-record identity. <c>null</c> means the identity does not exist; otherwise the
    /// stored <see cref="IngestionRecordProbe.ContentFingerprint"/> value is returned, which may
    /// itself be <c>null</c> — existence is never confused with the fingerprint value.
    /// </summary>
    Task<IngestionRecordProbe?> ProbeRecordAsync(
        SourceRecordId recordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new source record with first-delivery-canonical idempotency. Returns <c>true</c>
    /// when a new row was created, <c>false</c> when the delivery was a concurrent duplicate.
    /// </summary>
    Task<bool> InsertRecordAsync(
        SourceRecordId recordId,
        SourceRecordVersion? version,
        SourceProvenance provenance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the checkpoint using the observed-version CAS contract: <paramref name="observedVersion"/>
    /// is the version the caller read (<c>null</c> = absent row). A stale writer is rejected, never
    /// silently retried.
    /// </summary>
    Task SaveCheckpointAsync(
        AccountingSourceId sourceId,
        string capability,
        string cursorToken,
        DateTime updatedAtUtc,
        long? observedVersion,
        CancellationToken cancellationToken = default,
        SyncCheckpointState state = SyncCheckpointState.Active);

    /// <summary>Completes the run with its terminal outcome and processed count.</summary>
    Task FinishRunAsync(
        long runId,
        IngestionRunOutcome outcome,
        long recordsProcessed,
        string? error,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Existence + stored fingerprint value of a source-record identity. A <c>null</c> probe (not this
/// type) means the identity does not exist; an instance always means it exists, and
/// <see cref="ContentFingerprint"/> may itself be <c>null</c>.
/// </summary>
/// <param name="ContentFingerprint">The stored content fingerprint, or <c>null</c> when none was stored.</param>
public sealed record IngestionRecordProbe(string? ContentFingerprint);

/// <summary>Terminal outcome of a synchronization run (typed; mapped to storage by Infrastructure).</summary>
public enum IngestionRunOutcome
{
    /// <summary>The step completed successfully.</summary>
    Completed = 0,

    /// <summary>The step failed.</summary>
    Failed = 1,

    /// <summary>The step was cancelled.</summary>
    Cancelled = 2
}

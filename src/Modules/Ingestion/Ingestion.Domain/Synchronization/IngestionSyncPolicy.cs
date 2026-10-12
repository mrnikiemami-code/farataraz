namespace FaraTaraz.Modules.Ingestion.Domain.Synchronization;

using System;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.Ingestion.Contracts.Synchronization;

/// <summary>
/// Pure ingestion synchronization policy (Wave 2 domain extraction, task
/// <c>FT-REBASELINE-W2-DOMAIN-POLICY-001</c>).
///
/// These are the synchronization decisions that are independent of tenant authority, provider
/// I/O, time, MediatR, dependency injection and persistence. They were previously embedded in
/// <c>RunCustomerSyncHandler</c>; moving them here lets the Application use case stay an
/// orchestration that invokes Domain policy in the same order, with the same observable
/// exceptions.
///
/// <b>Deliberately NOT here:</b> tenant trust/agreement, source ownership, provider resolution,
/// the unit of work/transaction, checkpoint CAS orchestration, page-shape validation (already
/// constrained by <see cref="SyncBatch{TRecord}"/>), provenance timestamps and cancellation.
/// Those remain Application/Infrastructure responsibilities.
///
/// Purity is a hard constraint: this type performs no I/O, reads no ambient state, calls no
/// provider, uses no clock, and holds no dependency. These methods are therefore directly unit
/// testable and independent of the database.
/// </summary>
public static class IngestionSyncPolicy
{
    /// <summary>
    /// Rule (C5): an explicit restart begins a NEW synchronization and must never silently reuse a
    /// historical cursor. A restart carrying a caller-supplied cursor is rejected.
    /// </summary>
    /// <param name="isExplicitRestart">True only for an explicit restart intent.</param>
    /// <param name="requestedCursor">The caller-supplied cursor, when any.</param>
    /// <exception cref="InvalidSyncCursorException">A restart supplied a cursor.</exception>
    public static void EnsureExplicitRestartHasNoCursor(bool isExplicitRestart, SyncCursor? requestedCursor)
    {
        if (isExplicitRestart && requestedCursor is not null)
        {
            throw new InvalidSyncCursorException("An explicit restart must not supply a cursor.");
        }
    }

    /// <summary>
    /// Rule (C6): a durably <c>Completed</c> checkpoint is terminal. A normal continuation step
    /// must be refused before any provider call or write; only an explicit restart may pass it.
    /// </summary>
    /// <param name="isExplicitRestart">True only for an explicit restart intent.</param>
    /// <param name="observedState">The persisted checkpoint state, or <c>null</c> when none exists.</param>
    /// <param name="sourceId">The source whose synchronization is already completed.</param>
    /// <exception cref="SyncAlreadyCompletedException">Completed and not an explicit restart.</exception>
    public static void EnsureContinuationAllowed(
        bool isExplicitRestart,
        SyncCheckpointState? observedState,
        AccountingSourceId sourceId)
    {
        if (observedState == SyncCheckpointState.Completed && !isExplicitRestart)
        {
            throw new SyncAlreadyCompletedException(sourceId);
        }
    }

    /// <summary>
    /// Rule (C8): resumes from the persisted checkpoint cursor. When the caller also supplied a
    /// cursor it must agree, token-for-token, with the persisted one; otherwise the resume is
    /// refused (fail closed) rather than silently restarting or skipping a page. The returned
    /// request carries the persisted cursor scoped to the requested source/capability.
    /// </summary>
    /// <param name="request">The caller request being resumed.</param>
    /// <param name="persistedCursorToken">The opaque cursor token read from the persisted checkpoint.</param>
    /// <param name="capabilityName">The capability the persisted cursor belongs to.</param>
    /// <returns>The effective request, resuming from the persisted cursor.</returns>
    /// <exception cref="InvalidSyncCursorException">The caller cursor conflicts with the persisted one.</exception>
    public static SyncRequest ResumeFromPersistedCursor(
        SyncRequest request,
        string persistedCursorToken,
        string capabilityName)
    {
        ArgumentNullException.ThrowIfNull(request);

        var persistedCursor = new SyncCursor(
            persistedCursorToken, new SyncCursorScope(request.SourceId, capabilityName));

        if (request.Cursor is not null &&
            !string.Equals(request.Cursor.Token, persistedCursor.Token, StringComparison.Ordinal))
        {
            throw new InvalidSyncCursorException(
                "Caller-supplied cursor conflicts with the persisted checkpoint; refusing to resume.");
        }

        return new SyncRequest(request.SourceId, request.Mode, persistedCursor, request.BatchSize);
    }

    /// <summary>
    /// Rule (C11): an already-known source-record identity is classified by VALUE. An equal
    /// fingerprint — including <c>null == null</c> — is an idempotent duplicate; any inequality,
    /// including a stored-null versus a non-null incoming value (or the reverse), is a
    /// changed-record delivery that fails closed. A stored <c>null</c> fingerprint is never treated
    /// as "absent".
    /// </summary>
    /// <param name="storedFingerprint">The persisted content fingerprint (may be <c>null</c>).</param>
    /// <param name="incomingFingerprint">The incoming content fingerprint (may be <c>null</c>).</param>
    /// <param name="externalId">The source-scoped external identity, for the failure.</param>
    /// <exception cref="UnsupportedChangedRecordException">The identity exists with different content.</exception>
    public static void EnsureUnchangedOrIdempotent(
        string? storedFingerprint,
        string? incomingFingerprint,
        string externalId)
    {
        if (!string.Equals(storedFingerprint, incomingFingerprint, StringComparison.Ordinal))
        {
            throw new UnsupportedChangedRecordException(externalId);
        }
    }
}

namespace FaraTaraz.Modules.AccountingSources.Capabilities;

using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// A capability port that can be synchronized in bounded, resumable pages.
///
/// The contract is batch-oriented <c>Task&lt;SyncBatch&lt;TRecord&gt;&gt;</c> rather than
/// <c>IAsyncEnumerable&lt;&gt;</c> or <c>Task&lt;IReadOnlyList&lt;T&gt;&gt;</c> on purpose:
/// it preserves explicit checkpoint/restart semantics (a cursor per page), keeps each
/// page bounded in memory, and makes cancellation explicit.
/// </summary>
public interface ISyncablePort<TRecord> : ICapabilityPort
{
    /// <summary>
    /// Stable, capability-level identifier (e.g. "Customers", "Products"). Used to scope
    /// cursors and to validate resume; it is a platform namespace, never a provider value.
    /// </summary>
    string CapabilityName { get; }

    /// <summary>Declares which synchronization modes this capability supports.</summary>
    SyncModeSupport SyncModeSupport { get; }

    /// <summary>
    /// Synchronizes one bounded page for the requested source.
    ///
    /// - Propagates <paramref name="cancellationToken"/>; cancellation throws
    ///   <c>OperationCanceledException</c> and is never classified as a provider failure.
    /// - Throws <see cref="SyncModeNotSupportedException"/> if an unsupported mode is
    ///   requested; it never silently falls back.
    /// - Throws <see cref="InvalidSyncCursorException"/> /
    ///   <see cref="SyncCursorScopeMismatchException"/> on an invalid/mismatched resume.
    /// - Never returns null; an unsupported capability is expressed by the provider not
    ///   implementing this port at all (see <see cref="ProviderCapabilityExtensions"/>).
    /// </summary>
    Task<SyncBatch<TRecord>> SyncAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default);
}


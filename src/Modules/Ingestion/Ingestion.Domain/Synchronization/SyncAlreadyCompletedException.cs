namespace FaraTaraz.Modules.Ingestion.Domain.Synchronization;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Thrown when a synchronization step is requested for a source/capability whose persisted
/// checkpoint is already durably <c>Completed</c> and the caller did NOT request an explicit
/// restart (task W3-R3).
///
/// A completed synchronization is terminal: it must never be resumed or implicitly replayed. Only
/// an explicit restart intent may begin a new synchronization over a completed checkpoint.
/// </summary>
public sealed class SyncAlreadyCompletedException : Exception
{
    /// <summary>Creates the exception for the completed source.</summary>
    public SyncAlreadyCompletedException(AccountingSourceId sourceId)
        : base($"The synchronization for source '{sourceId}' is already completed; " +
               "an explicit restart is required to begin a new synchronization.")
    {
        SourceId = sourceId;
    }

    /// <summary>The source whose synchronization is already completed.</summary>
    public AccountingSourceId SourceId { get; }
}

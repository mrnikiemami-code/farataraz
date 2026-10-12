namespace FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// State of a persisted synchronization checkpoint (task M5/R3).
///
/// It distinguishes an in-progress synchronization from a durably completed one, so a terminal
/// page can transition the checkpoint to a non-resumable marker instead of leaving a stale
/// continuation cursor behind.
/// </summary>
public enum SyncCheckpointState
{
    /// <summary>A synchronization is in progress; the persisted cursor is a resumable continuation.</summary>
    Active = 0,

    /// <summary>The synchronization reached a terminal page; the checkpoint is NOT resumable.</summary>
    Completed = 1
}

namespace FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Thrown when a source record with an already-known identity arrives with DIFFERENT content
/// (a changed-record / new-version delivery).
///
/// W2 persistence is <b>first-delivery-canonical</b> (ADR-010 decision 7): the same identity is
/// stored exactly once and identical duplicates are ignored. Changed-record freshness
/// (re-ingesting an updated version of an already-stored identity) is <b>not</b> supported yet —
/// it is pending the open FT-DATA-003 decision. The engine therefore fails closed instead of
/// silently discarding an update (Constitution E.21: sync semantics are explicit).
/// </summary>
public sealed class UnsupportedChangedRecordException : Exception
{
    /// <summary>Creates the exception for the record whose changed content was rejected.</summary>
    public UnsupportedChangedRecordException(string externalId)
        : base($"Source record '{externalId}' already exists with different content; " +
               "changed-record freshness is not supported (FT-DATA-003 pending).")
    {
        ExternalId = externalId;
    }

    /// <summary>The source-scoped external identity of the rejected record.</summary>
    public string ExternalId { get; }
}

namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

/// <summary>
/// Thrown by <see cref="SyncCheckpointRepository.SaveAsync"/> when a checkpoint write is rejected
/// because a newer checkpoint was committed for the same scope since the writer read it — i.e. the
/// writer would have overwritten a newer checkpoint with stale progress (FT-DATA-001).
///
/// The newer checkpoint is preserved (never overwritten). The caller must re-read the authoritative
/// cursor and retry, rather than applying a stale cursor. This failure is a concurrency guard, not a
/// data-integrity failure: it surfaces a lost-update race instead of letting it regress a cursor.
/// </summary>
public sealed class StaleCheckpointException : Exception
{
    /// <summary>The trusted tenant whose checkpoint write was rejected.</summary>
    public string TenantId { get; }

    /// <summary>The accounting source whose checkpoint write was rejected.</summary>
    public string SourceId { get; }

    /// <summary>The capability scope whose checkpoint write was rejected.</summary>
    public string Capability { get; }

    /// <summary>The version the writer expected (the version it last observed).</summary>
    public long ExpectedVersion { get; }

    /// <summary>The version actually stored when the write was rejected (a newer version).</summary>
    public long StoredVersion { get; }

    /// <summary>
    /// Creates a stale-checkpoint rejection for the given scope and versions.
    /// </summary>
    public StaleCheckpointException(
        string tenantId,
        string sourceId,
        string capability,
        long expectedVersion,
        long storedVersion)
        : base(
            $"Checkpoint write rejected for scope " +
            $"({tenantId}/{sourceId}/{capability}): a newer checkpoint " +
            $"(version {storedVersion}) was committed since the writer observed " +
            $"version {expectedVersion}. The newer checkpoint was preserved; re-read the " +
            $"authoritative cursor and retry.")
    {
        TenantId = tenantId;
        SourceId = sourceId;
        Capability = capability;
        ExpectedVersion = expectedVersion;
        StoredVersion = storedVersion;
    }
}

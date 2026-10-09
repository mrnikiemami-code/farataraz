namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping of one synchronization run (run state).
///
/// Tenant-scoped: every column is bounded by <see cref="TenantId"/>. The row lifecycle is
/// <c>Running</c> → <c>Completed</c> / <c>Failed</c> / <c>Cancelled</c>. The failure state
/// (status, error, processed count) is stored here for W3 to consume; W2 does not implement
/// retry loops (ADR-010 decision 11).
/// </summary>
public sealed class SyncRunEntity
{
    /// <summary>Generated row identity (surrogate key).</summary>
    public long Id { get; set; }

    /// <summary>Trusted tenant that owns this run. Never null.</summary>
    public string TenantId { get; set; } = default!;

    /// <summary>The accounting source being synchronized.</summary>
    public string SourceId { get; set; } = default!;

    /// <summary>Capability scope (e.g. "Customers").</summary>
    public string Capability { get; set; } = default!;

    /// <summary>Requested mode, stored as its name ("Full" / "Incremental").</summary>
    public string Mode { get; set; } = default!;

    /// <summary>Run state ("Running" / "Completed" / "Failed" / "Cancelled").</summary>
    public string State { get; set; } = default!;

    /// <summary>Records processed so far in this run.</summary>
    public long RecordsProcessed { get; set; }

    /// <summary>Platform time the run started.</summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>Platform time the run reached a terminal state (null while running).</summary>
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>Last error text, when the run failed. Nullable.</summary>
    public string? Error { get; set; }
}

namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping of one synchronization checkpoint (resumption cursor).
///
/// One checkpoint per (tenant, source, capability). The opaque <see cref="CursorToken"/> is
/// provider-internal and must never be interpreted by the platform; its platform scope
/// (source + capability) is what resume validation checks (ADR-010 decision 9). A UNIQUE
/// constraint on (TenantId, SourceId, Capability) stores the cursor exactly once per scope.
/// </summary>
public sealed class SyncCheckpointEntity
{
    /// <summary>Generated row identity (surrogate key).</summary>
    public long Id { get; set; }

    /// <summary>Trusted tenant that owns this checkpoint. Never null.</summary>
    public string TenantId { get; set; } = default!;

    /// <summary>The accounting source this checkpoint belongs to.</summary>
    public string SourceId { get; set; } = default!;

    /// <summary>Capability scope (e.g. "Customers"). Never null.</summary>
    public string Capability { get; set; } = default!;

    /// <summary>Opaque provider cursor token. Never null.</summary>
    public string CursorToken { get; set; } = default!;

    /// <summary>Platform time the cursor was last written.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

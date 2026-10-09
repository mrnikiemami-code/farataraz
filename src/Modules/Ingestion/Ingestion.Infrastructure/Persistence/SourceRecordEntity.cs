namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping of a synchronized source record (identity + provenance).
///
/// This is the durable idempotency key of the ingestion pipeline. The tuple
/// <c>(TenantId, SourceId, RecordKind, ExternalId)</c> is UNIQUE, so delivering the same
/// source record twice cannot create duplicate state (ADR-010 decision 7, Constitution E.20).
/// The record also carries the version/change signal and lightweight provenance needed for
/// reconciliation, debugging, remapping, and auditing (ADR-005, ADR-010 decision 7).
///
/// Note: <c>ExternalId</c> here is the record-external id (source-scoped), distinct from a
/// canonical id. Equal external ids across sources do NOT prove equal entities (Constitution
/// B.10 / ADR-004).
/// </summary>
public sealed class SourceRecordEntity
{
    /// <summary>Generated row identity (surrogate key).</summary>
    public long Id { get; set; }

    /// <summary>Trusted tenant that owns this record. Never null.</summary>
    public string TenantId { get; set; } = default!;

    /// <summary>The accounting source the record was synchronized from.</summary>
    public string SourceId { get; set; } = default!;

    /// <summary>Platform record kind (e.g. "Customer", "Product", "Invoice"). Never null.</summary>
    public string RecordKind { get; set; } = default!;

    /// <summary>Source-scoped external id. Never null.</summary>
    public string ExternalId { get; set; } = default!;

    /// <summary>Deterministic content fingerprint (optional change signal).</summary>
    public string? ContentFingerprint { get; set; }

    /// <summary>Provider-supplied revision marker (optional).</summary>
    public string? ProviderRevision { get; set; }

    /// <summary>Provider-reported modification time (optional; distinct from retrieval time).</summary>
    public DateTime? ProviderModifiedAtUtc { get; set; }

    /// <summary>Platform-observed retrieval time.</summary>
    public DateTime RetrievedAtUtc { get; set; }

    /// <summary>Provider identity that produced this record (provenance).</summary>
    public string? Provider { get; set; }

    /// <summary>Checkpoint/cursor this record was ingested under (provenance).</summary>
    public string? Checkpoint { get; set; }
}

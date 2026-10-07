namespace FaraTaraz.Core.Synchronization;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Generic, provider-independent source-side identity of a synchronizable record.
///
/// <c>AccountingSourceId + RecordKind + ExternalId</c>. The <em>record kind</em> is a
/// platform-level semantic namespace (e.g. "Customer", "Product", "Invoice"), NOT a
/// provider field. It is required so that a Product external code and an Invoice external
/// code with identical text are NOT confused during generic ingestion deduplication.
///
/// Identity is deterministic: it never uses random values, and it never embeds a platform
/// retrieval timestamp (that belongs to provenance, not identity).
/// </summary>
public sealed record SourceRecordId(AccountingSourceId SourceId, string RecordKind, string ExternalId)
{
    public override string ToString() => $"{SourceId}::{RecordKind}![{ExternalId}]";
}

namespace FaraTaraz.Modules.Ingestion.Domain.Synchronization;

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
public sealed record SourceRecordId
{
    public AccountingSourceId SourceId { get; }

    public string RecordKind { get; }

    public string ExternalId { get; }

    public SourceRecordId(AccountingSourceId sourceId, string recordKind, string externalId)
    {
        if (string.IsNullOrWhiteSpace(recordKind))
        {
            throw new InvalidSourceRecordIdException(
                "Source record kind must be a non-empty string.");
        }

        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new InvalidSourceRecordIdException(
                "Source record external id must be a non-empty string.");
        }

        if (string.IsNullOrWhiteSpace(sourceId.Value))
        {
            throw new InvalidSourceRecordIdException("Source record source id must be a non-empty identity.");
        }

        SourceId = sourceId;
        RecordKind = recordKind;
        ExternalId = externalId;
    }

    public override string ToString() => $"{SourceId}::{RecordKind}![{ExternalId}]";
}

/// <summary>
/// Thrown when a <see cref="SourceRecordId"/> is malformed (empty/whitespace record kind or
/// external id). This is a caller-input validation error, never a provider failure.
/// </summary>
public sealed class InvalidSourceRecordIdException : Exception
{
    public InvalidSourceRecordIdException(string message) : base(message)
    {
    }
}

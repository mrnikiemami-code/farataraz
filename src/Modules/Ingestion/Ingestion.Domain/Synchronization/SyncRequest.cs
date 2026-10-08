namespace FaraTaraz.Modules.Ingestion.Domain.Synchronization;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Provider-independent request that tells a capability how to execute synchronization.
///
/// It carries ONLY what the capability needs: the accounting source, the requested mode,
/// an optional resume cursor, and an optional batch limit.
///
/// It deliberately does NOT carry a <c>TenantId</c>. The provider receives an
/// <see cref="AccountingSourceId"/> for source access, but that does not imply
/// authorization; proving that the trusted Tenant owns the source is the orchestration
/// layer's responsibility (W3), never the adapter's. It also contains no provider-specific
/// fields.
/// </summary>
public sealed record SyncRequest
{
    public AccountingSourceId SourceId { get; }

    public SyncMode Mode { get; }

    public SyncCursor? Cursor { get; }

    public int? BatchSize { get; }

    public SyncRequest(AccountingSourceId sourceId, SyncMode mode, SyncCursor? cursor = null, int? batchSize = null)
    {
        if (batchSize is { } requested && requested <= 0)
        {
            throw new InvalidSyncRequestException(
                $"SyncRequest.BatchSize must be a positive integer when provided, but was '{requested}'.");
        }

        if (string.IsNullOrWhiteSpace(sourceId.Value))
        {
            throw new InvalidSyncRequestException("SyncRequest.SourceId must be a non-empty identity.");
        }

        SourceId = sourceId;
        Mode = mode;
        Cursor = cursor;
        BatchSize = batchSize;
    }
}

/// <summary>
/// Thrown when a <see cref="SyncRequest"/> is malformed (e.g. a non-positive batch size).
/// This is a caller-input validation error, never a provider failure.
/// </summary>
public sealed class InvalidSyncRequestException : Exception
{
    public InvalidSyncRequestException(string message) : base(message)
    {
    }
}

namespace FaraTaraz.Core.Synchronization;

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
        SourceId = sourceId;
        Mode = mode;
        Cursor = cursor;
        BatchSize = batchSize;
    }
}

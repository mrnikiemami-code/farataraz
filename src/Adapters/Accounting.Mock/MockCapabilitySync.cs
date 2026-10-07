namespace FaraTaraz.Adapters.Accounting.Mock;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Core.Accounting;
using FaraTaraz.Core.SourceModel;
using FaraTaraz.Core.Synchronization;

/// <summary>
/// Deterministic, in-memory synchronization engine for one capability.
///
/// This is a TEST provider: it is NOT a fake production database. It exists to prove the
/// provider-independent synchronization contract (W1) across controlled, repeatable
/// scenarios (normal paging, empty, duplicate, changed record, transient failure,
/// cancellation, resume). It is deterministic — no random data.
///
/// Model: a flat, ordered dataset per capability. A cursor token encodes the next record
/// index; each call returns a bounded chunk (page size or <c>request.BatchSize</c>) and a
/// continuation cursor until the dataset is exhausted.
/// </summary>
public sealed class MockCapabilitySync<TRecord>
{
    /// <summary>Stable capability identifier, matches the port's <c>CapabilityName</c>.</summary>
    public string CapabilityName { get; }

    /// <summary>Declared sync-mode support for this capability.</summary>
    public SyncModeSupport ModeSupport { get; }

    /// <summary>Full ordered dataset. May be empty for a supported-but-empty capability.</summary>
    public IReadOnlyList<TRecord> Records { get; }

    /// <summary>Records returned per page by default when the request omits a batch size.</summary>
    public int PageSize { get; }

    /// <summary>Number of initial attempts that fail with a transient error before success.</summary>
    public int TransientFailuresBeforeSuccess { get; }

    private int _transientFailuresRemaining;

    public MockCapabilitySync(
        string capabilityName,
        IReadOnlyList<TRecord> records,
        SyncModeSupport modeSupport,
        int pageSize = 2,
        int transientFailuresBeforeSuccess = 0)
    {
        CapabilityName = capabilityName;
        Records = records;
        ModeSupport = modeSupport;
        PageSize = pageSize;
        TransientFailuresBeforeSuccess = transientFailuresBeforeSuccess;
        _transientFailuresRemaining = transientFailuresBeforeSuccess;
    }

    public async Task<SyncBatch<TRecord>> SyncAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        // Cancellation is caller-requested and must propagate; it is NOT a provider failure.
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModeSupport.Supports(request.Mode))
        {
            throw new SyncModeNotSupportedException(request.Mode);
        }

        // A resumed cursor must belong to this source/capability; never silently restart.
        SyncCursors.ValidateResume(request, CapabilityName);

        if (_transientFailuresRemaining > 0)
        {
            _transientFailuresRemaining--;
            throw new SyncTransientException(
                $"Mock capability '{CapabilityName}' simulated transient failure {_transientFailuresRemaining + 1}.");
        }

        int start = ResolveStartIndex(request);
        int limit = request.BatchSize is { } b && b > 0 ? b : PageSize;

        var page = new List<TRecord>();
        for (var index = start; index < Records.Count && page.Count < limit; index++)
        {
            page.Add(Records[index]);
        }

        bool complete = start + page.Count >= Records.Count;
        string? nextToken = complete ? null : (start + page.Count).ToString();

        var scope = new SyncCursorScope(request.SourceId, CapabilityName);
        return complete
            ? SyncBatch<TRecord>.Complete(page)
            : SyncBatch<TRecord>.Continue(page, new SyncCursor(nextToken!, scope));
    }

    private int ResolveStartIndex(SyncRequest request)
    {
        if (request.Cursor is null)
        {
            return 0;
        }

        if (!int.TryParse(request.Cursor.Token, out var index) || index < 0 || index > Records.Count)
        {
            throw new InvalidSyncCursorException(
                $"Cursor token '{request.Cursor.Token}' is not a valid continuation for '{CapabilityName}'.");
        }

        return index;
    }
}

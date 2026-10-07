namespace FaraTaraz.Core.Synchronization;

/// <summary>
/// Bounded, provider-independent synchronization result for one page.
///
/// Invariant (enforced by construction): a <b>complete</b> batch must NOT carry a
/// continuation cursor, and a <b>continuing</b> batch MUST carry one. Use the factory
/// methods rather than the constructor to keep this invariant.
/// </summary>
public sealed record SyncBatch<TRecord>
{
    /// <summary>Bounded set of records for this page. Never null.</summary>
    public IReadOnlyList<TRecord> Records { get; }

    /// <summary>
    /// Continuation boundary for the next page. Present only when <see cref="IsComplete"/>
    /// is false; null when this is the final page.
    /// </summary>
    public SyncCursor? NextCursor { get; }

    /// <summary>True when this is the final page of the synchronization.</summary>
    public bool IsComplete { get; }

    private SyncBatch(IReadOnlyList<TRecord> records, SyncCursor? nextCursor, bool isComplete)
    {
        if (isComplete && nextCursor is not null)
        {
            throw new ArgumentException(
                "A complete batch must not carry a continuation cursor.");
        }

        if (!isComplete && nextCursor is null)
        {
            throw new ArgumentException(
                "A continuing batch must carry a continuation cursor.");
        }

        Records = records ?? throw new ArgumentNullException(nameof(records));
        NextCursor = nextCursor;
        IsComplete = isComplete;
    }

    /// <summary>Final page: all records, no continuation.</summary>
    public static SyncBatch<TRecord> Complete(IReadOnlyList<TRecord> records)
        => new(records, null, true);

    /// <summary>Intermediate page: records plus the cursor to resume from.</summary>
    public static SyncBatch<TRecord> Continue(IReadOnlyList<TRecord> records, SyncCursor nextCursor)
        => new(records, nextCursor, false);

    /// <summary>Empty final page for a supported capability with zero records.</summary>
    public static SyncBatch<TRecord> Empty()
        => Complete(Array.Empty<TRecord>());
}

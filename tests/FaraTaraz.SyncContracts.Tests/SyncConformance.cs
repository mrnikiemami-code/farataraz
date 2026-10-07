namespace FaraTaraz.SyncContracts.Tests;

using System.Collections.Generic;
using System.Linq;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Core.Accounting;
using FaraTaraz.Core.Synchronization;

/// <summary>
/// Reusable, adapter-agnostic conformance assertions for the synchronization contract.
///
/// Every method is parameterized over a <see cref="SyncConformanceScenario{TRecord}"/> so any
/// provider (Mock, Asan, Sepidar, …) is exercised against the SAME behavioral contract. The
/// harness only observes the public <c>ISyncablePort&lt;*&gt;</c> surface and the neutral
/// contract types; it never couples to a specific adapter's internals.
/// </summary>
public static class SyncConformance
{
    /// <summary>
    /// Drives a full synchronization to completion, asserting:
    /// - every page is bounded by <c>scenario.PageSize</c>;
    /// - completion is coherent with the presence/absence of a continuation cursor;
    /// - pages are contiguous and cover the dataset exactly, in order.
    /// </summary>
    public static async Task FullSyncReturnsBoundedPages<TRecord>(
        IAccountingProvider provider,
        AccountingCapability capability,
        SyncConformanceScenario<TRecord> scenario,
        CancellationToken cancellationToken = default)
        where TRecord : notnull
    {
        var port = provider.Resolve<TRecord>(capability);

        var collected = new List<TRecord>();
        SyncCursor? cursor = null;

        do
        {
            var batch = await port.SyncAsync(
                new SyncRequest(scenario.SourceId, SyncMode.Full, cursor), cancellationToken);

            Assert.True(
                batch.Records.Count <= scenario.PageSize,
                "Each page must be bounded by the page size.");

            Assert.True(
                batch.IsComplete == (batch.NextCursor is null),
                "Completion must be coherent with the presence of a continuation cursor.");

            collected.AddRange(batch.Records);
            cursor = batch.NextCursor;
        } while (cursor is not null);

        Assert.Equal(scenario.Dataset, collected);
    }

    /// <summary>
    /// Asserts incremental behavior matches the declared <c>SyncModeSupport</c>: a provider
    /// that declares Incremental accepts it; one that does not fails explicitly (never
    /// silently falling back to Full).
    /// </summary>
    public static async Task IncrementalBehaviorMatchesDeclaration<TRecord>(
        IAccountingProvider provider,
        AccountingCapability capability,
        SyncConformanceScenario<TRecord> scenario,
        CancellationToken cancellationToken = default)
        where TRecord : notnull
    {
        var port = provider.Resolve<TRecord>(capability);

        if (scenario.ModeSupport.SupportsIncremental)
        {
            var batch = await port.SyncAsync(
                scenario.Request(SyncMode.Incremental), cancellationToken);

            Assert.True(
                batch.Records.Count > 0,
                "Incremental of a non-empty dataset should return at least one page.");
        }
        else
        {
            await Assert.ThrowsAsync<SyncModeNotSupportedException>(
                () => port.SyncAsync(scenario.Request(SyncMode.Incremental), cancellationToken));
        }
    }

    /// <summary>
    /// Cancellation propagates as <c>OperationCanceledException</c> and is NEVER classified as
    /// a provider failure (i.e. it is not a <c>SyncProviderException</c>).
    /// </summary>
    public static async Task CancellationPropagatesAndIsNotAProviderFailure<TRecord>(
        IAccountingProvider provider,
        AccountingCapability capability,
        SyncConformanceScenario<TRecord> scenario,
        CancellationToken cancellationToken = default)
        where TRecord : notnull
    {
        var port = provider.Resolve<TRecord>(capability);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAsync<OperationCanceledException>(
            () => port.SyncAsync(scenario.Request(SyncMode.Full), cts.Token));

        Assert.IsNotType<SyncProviderException>(ex);
    }

    /// <summary>
    /// An explicit batch size is honored: no returned page exceeds the requested size.
    /// </summary>
    public static async Task BatchSizeIsHonored<TRecord>(
        IAccountingProvider provider,
        AccountingCapability capability,
        SyncConformanceScenario<TRecord> scenario,
        CancellationToken cancellationToken = default)
        where TRecord : notnull
    {
        var port = provider.Resolve<TRecord>(capability);
        const int requested = 1;

        var batch = await port.SyncAsync(
            scenario.Request(SyncMode.Full, batchSize: requested), cancellationToken);

        Assert.All(batch.Records, r => Assert.True(r is not null));
        Assert.True(batch.Records.Count <= requested, "Page must not exceed the requested batch size.");
    }

    /// <summary>
    /// Cursor continuation resumes from the correct logical position without restarting.
    /// </summary>
    public static async Task CursorContinuationWorks<TRecord>(
        IAccountingProvider provider,
        AccountingCapability capability,
        SyncConformanceScenario<TRecord> scenario,
        CancellationToken cancellationToken = default)
        where TRecord : notnull
    {
        var port = provider.Resolve<TRecord>(capability);

        var first = await port.SyncAsync(
            scenario.Request(SyncMode.Full), cancellationToken);

        Assert.False(first.IsComplete, "A multi-page scenario must not complete on the first page.");
        Assert.NotNull(first.NextCursor);

        var second = await port.SyncAsync(
            scenario.Request(SyncMode.Full, first.NextCursor), cancellationToken);

        // The second page must continue where the first left off, not restart.
        var expected = scenario.Dataset.Skip(first.Records.Count).Take(second.Records.Count).ToList();
        Assert.Equal(expected, second.Records);
    }

    /// <summary>
    /// A supported-but-empty capability returns a successful empty completion — NOT an
    /// unsupported-capability failure.
    /// </summary>
    public static async Task ValidEmptySourceReturnsEmptyCompletion<TRecord>(
        IAccountingProvider provider,
        AccountingCapability capability,
        SyncConformanceScenario<TRecord> scenario,
        CancellationToken cancellationToken = default)
        where TRecord : notnull
    {
        var port = provider.Resolve<TRecord>(capability);

        var batch = await port.SyncAsync(scenario.Request(SyncMode.Full), cancellationToken);

        Assert.True(batch.IsComplete);
        Assert.Empty(batch.Records);
        Assert.Null(batch.NextCursor);
    }

    /// <summary>
    /// Resolving an undeclared capability fails explicitly (throws), never returning an empty
    /// collection. This is the same "unsupported ≠ empty" invariant the W0 capability model
    /// encodes, exercised through the real resolution path.
    /// </summary>
    public static void UnsupportedCapabilityFailsExplicitly<TRecord>(
        IAccountingProvider provider,
        AccountingCapability capability)
        where TRecord : notnull
    {
        var ex = Assert.Throws<CapabilityNotSupportedException>(
            () => provider.Resolve<TRecord>(capability));

        Assert.Equal(capability, ex.Capability);
    }
}

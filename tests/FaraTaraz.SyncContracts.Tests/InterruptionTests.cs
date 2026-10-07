namespace FaraTaraz.SyncContracts.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Core.Accounting;
using FaraTaraz.Core.SourceModel;
using FaraTaraz.Core.Synchronization;
using Xunit;

/// <summary>
/// Interruption semantics (task §21): retrieve batch 1, process it, capture the cursor,
/// STOP before batch 2, then RESTART using the stored cursor. The provider must continue from
/// the correct logical position — no accidental restart. W1 uses in-memory state only; durable
/// checkpoint persistence belongs to W2.
/// </summary>
public class InterruptionTests
{
    private static MockAccountingProvider ProviderWith5Customers() => MockSyncScenarios.ProviderWith5Customers();

    [Fact]
    public async Task InterruptAfterBatch1_AndRestartFromCursor_ContinuesWithoutRestart()
    {
        var provider = ProviderWith5Customers();
        var port = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);

        // 1. Retrieve batch 1.
        var batch1 = await port.SyncAsync(new SyncRequest(SourceIds.Source, SyncMode.Full));
        Assert.Equal(new[] { "C1", "C2" }, batch1.Records.Select(r => r.Code));

        // 2. Caller considers batch 1 processed.
        // 3. Capture the continuation cursor.
        var storedCursor = batch1.NextCursor;
        Assert.NotNull(storedCursor);

        // 4. Execution stops before batch 2 (simulated by discarding further work).

        // 5. Caller restarts using the stored cursor.
        var resumed = await port.SyncAsync(
            new SyncRequest(SourceIds.Source, SyncMode.Full, storedCursor));

        // 6. Provider continues from index 2 — NOT from index 0.
        Assert.Equal(new[] { "C3", "C4" }, resumed.Records.Select(r => r.Code));
    }

    [Fact]
    public async Task FullRestartAfterPartialProgress_ReachesCompleteDataset()
    {
        var provider = ProviderWith5Customers();
        var port = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);

        var seen = new List<string>();

        // Simulate a multi-step run that is interrupted, then resumed from the saved cursor.
        SyncCursor? savedCursor = null;

        for (var page = 0; page < 3; page++)
        {
            SyncCursor? resume = savedCursor;
            var batch = await port.SyncAsync(
                new SyncRequest(SourceIds.Source, SyncMode.Full, resume));

            foreach (var record in batch.Records)
            {
                seen.Add(record.Code);
            }

            savedCursor = batch.NextCursor;

            // Interrupt after the first page (before batch 2).
            if (page == 0)
            {
                break;
            }
        }

        // Resume to completion.
        while (savedCursor is not null)
        {
            var batch = await port.SyncAsync(
                new SyncRequest(SourceIds.Source, SyncMode.Full, savedCursor));

            foreach (var record in batch.Records)
            {
                seen.Add(record.Code);
            }

            savedCursor = batch.NextCursor;
        }

        Assert.Equal(
            new[] { "C1", "C2", "C3", "C4", "C5" },
            seen);
    }
}

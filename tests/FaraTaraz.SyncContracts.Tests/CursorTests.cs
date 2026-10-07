namespace FaraTaraz.SyncContracts.Tests;

using System.Collections.Generic;
using System.Threading.Tasks;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Core.Accounting;
using FaraTaraz.Core.SourceModel;
using FaraTaraz.Core.Synchronization;
using Xunit;

/// <summary>
/// Cursor / checkpoint contract evidence (task §20): resume, completion, wrong source,
/// wrong capability, and invalid cursor. The Mock enforces scope validation via
/// <see cref="SyncCursors.ValidateResume"/> and token validation via its own index parser.
/// </summary>
public class CursorTests
{
    private static MockAccountingProvider ProviderWith5Customers() => MockSyncScenarios.ProviderWith5Customers();

    [Fact]
    public async Task Resume_ContinuesFromCorrectPositionWithoutRestart()
    {
        var provider = ProviderWith5Customers();
        var port = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);

        var first = await port.SyncAsync(new SyncRequest(SourceIds.Source, SyncMode.Full));
        Assert.Equal(2, first.Records.Count); // page size 2
        var cursor = first.NextCursor;
        Assert.NotNull(cursor);

        var resumed = await port.SyncAsync(
            new SyncRequest(SourceIds.Source, SyncMode.Full, cursor));

        // The resumed page continues at index 2, not index 0.
        Assert.Equal("C3", resumed.Records[0].Code);
        Assert.Equal(2, resumed.Records.Count);
    }

    [Fact]
    public async Task Completion_FinalPageHasCoherentCompletionSemantics()
    {
        var provider = ProviderWith5Customers();
        var port = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);

        // Drive to the final page.
        SyncCursor? cursor = null;
        SyncBatch<SourceCustomer> last = null!;

        do
        {
            var batch = await port.SyncAsync(new SyncRequest(SourceIds.Source, SyncMode.Full, cursor));
            last = batch;
            cursor = batch.NextCursor;
        } while (cursor is not null);

        Assert.True(last.IsComplete);
        Assert.Null(last.NextCursor);
        // Final page carries the tail of the dataset (index 4: C5).
        Assert.Single(last.Records);
        Assert.Equal("C5", last.Records[0].Code);
    }

    [Fact]
    public async Task WrongSource_RejectedExplicitly()
    {
        var provider = ProviderWith5Customers();
        var port = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);

        // A cursor created for a DIFFERENT source must not be silently accepted.
        var foreignCursor = new SyncCursor(
            "2",
            new SyncCursorScope(new AccountingSourceId("src-mock-2"), MockAccountingProvider.CustomersCapability));

        var ex = await Assert.ThrowsAsync<SyncCursorScopeMismatchException>(
            () => port.SyncAsync(
                new SyncRequest(SourceIds.Source, SyncMode.Full, foreignCursor)));

        Assert.Contains("source", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongCapability_RejectedExplicitly()
    {
        var provider = ProviderWith5Customers();

        // A cursor scoped to Products must not silently become a Customer cursor.
        var productCursor = new SyncCursor(
            "2",
            new SyncCursorScope(SourceIds.Source, MockAccountingProvider.ProductsCapability));

        var customers = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);

        var ex = await Assert.ThrowsAsync<SyncCursorScopeMismatchException>(
            () => customers.SyncAsync(
                new SyncRequest(SourceIds.Source, SyncMode.Full, productCursor)));

        Assert.Contains("capability", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidCursorToken_FailsExplicitly_NotSilentRestart()
    {
        var provider = ProviderWith5Customers();
        var port = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);

        var ex = await Assert.ThrowsAsync<InvalidSyncCursorException>(
            () => port.SyncAsync(
                new SyncRequest(
                    SourceIds.Source,
                    SyncMode.Full,
                    new SyncCursor("not-a-number", new SyncCursorScope(SourceIds.Source, MockAccountingProvider.CustomersCapability)))));

        Assert.Contains("not a valid continuation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptyCursorToken_IsRejectedAtConstruction()
    {
        Assert.Throws<InvalidSyncCursorException>(
            () => new SyncCursor("", new SyncCursorScope(SourceIds.Source, "Customers")));
    }
}

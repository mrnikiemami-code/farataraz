namespace FaraTaraz.SyncContracts.Tests;

using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Xunit;

/// <summary>
/// Contract/compatibility evidence that <see cref="SyncCursorScope"/> retains value equality
/// and deconstruction after the W1-R2 sync contract hardening.
///
/// These structural guarantees are relied on by orchestration (cursor-scope comparison) and
/// by callers that deconstruct the source/capability pair. They must hold regardless of the
/// secondary validating constructor.
/// </summary>
public sealed class SyncCursorScopeContractTests
{
    private static readonly AccountingSourceId SourceA = new("src-a");
    private static readonly AccountingSourceId SourceB = new("src-b");

    [Fact]
    public void Same_source_and_capability_are_value_equal()
    {
        var x = new SyncCursorScope(SourceA, "Customers");
        var y = new SyncCursorScope(SourceA, "Customers");

        Assert.Equal(x, y);
        Assert.True(x == y);
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Fact]
    public void Different_source_is_not_value_equal()
    {
        Assert.NotEqual(
            new SyncCursorScope(SourceA, "Customers"),
            new SyncCursorScope(SourceB, "Customers"));
    }

    [Fact]
    public void Different_capability_is_not_value_equal()
    {
        Assert.NotEqual(
            new SyncCursorScope(SourceA, "Customers"),
            new SyncCursorScope(SourceA, "Products"));
    }

    [Fact]
    public void Deconstructs_into_source_and_capability()
    {
        var scope = new SyncCursorScope(SourceA, "Customers");

        var (source, capability) = scope;

        Assert.Equal(SourceA, source);
        Assert.Equal("Customers", capability);
    }
}

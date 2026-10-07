namespace FaraTaraz.ArchitectureTests;

using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Core.Accounting;
using FaraTaraz.Core.SourceModel;
using Xunit;

/// <summary>
/// Provider capabilities are explicit and independent. An unsupported capability is
/// an explicit failure (throws), never an empty collection.
/// </summary>
public class CapabilityModelTests
{
    private static MockAccountingProvider NewProvider() => new(
        AccountingCapability.Customers | AccountingCapability.Products,
        new MockCustomerSource(Array.Empty<SourceCustomer>()),
        new MockProductSource(Array.Empty<SourceProduct>()));

    [Fact]
    public void Supported_capability_is_declared_and_resolvable()
    {
        var provider = NewProvider();

        Assert.True(provider.Supports(AccountingCapability.Customers));
        Assert.True(provider.Supports(AccountingCapability.Products));

        var customers = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);
        Assert.IsAssignableFrom<ICustomerSource>(customers);
    }

    [Fact]
    public void Unsupported_capability_throws_explicitly_and_is_never_empty()
    {
        var provider = NewProvider();

        Assert.False(provider.Supports(AccountingCapability.Inventory));

        var ex = Assert.Throws<CapabilityNotSupportedException>(
            () => provider.RequireCapability<IInventorySource>(AccountingCapability.Inventory));

        Assert.Equal(AccountingCapability.Inventory, ex.Capability);
    }
}

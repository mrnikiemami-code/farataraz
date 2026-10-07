namespace FaraTaraz.BuildingBlocks.Tests;

using FaraTaraz.BuildingBlocks.Accounting;
using Xunit;

public class CapabilityTests
{
    [Fact]
    public void Capabilities_are_independent_flags()
    {
        var caps = AccountingCapability.Customers | AccountingCapability.Products;

        Assert.True(caps.HasFlag(AccountingCapability.Customers));
        Assert.True(caps.HasFlag(AccountingCapability.Products));
        Assert.False(caps.HasFlag(AccountingCapability.Inventory));
        Assert.False(caps.HasFlag(AccountingCapability.Sales));
    }

    [Fact]
    public void A_partial_capability_set_does_not_imply_full_support()
    {
        // Customers + Products supported, Inventory not declared.
        var caps = AccountingCapability.Customers | AccountingCapability.Products;

        Assert.False(caps.HasFlag(AccountingCapability.Inventory));
        Assert.False(caps.HasFlag(AccountingCapability.Payments));
    }
}

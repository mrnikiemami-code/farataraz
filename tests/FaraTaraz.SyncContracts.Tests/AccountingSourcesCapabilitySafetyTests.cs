namespace FaraTaraz.SyncContracts.Tests;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Modules.AccountingSources;
using Xunit;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;

public sealed class AccountingSourcesCapabilitySafetyTests
{
    private sealed class DeclaredProvider : IAccountingProvider
    {
        public AccountingCapability SupportedCapabilities { get; init; }
    }

    [Fact]
    public void None_and_unknown_flags_are_never_supported()
    {
        IAccountingProvider provider = new DeclaredProvider
        {
            SupportedCapabilities = AccountingCapability.Customers | AccountingCapability.Products
        };
        Assert.False(provider.Supports(AccountingCapability.None));
        Assert.False(provider.Supports((AccountingCapability)128));
        Assert.False(provider.Supports(AccountingCapability.Customers | (AccountingCapability)128));
        Assert.True(provider.Supports(AccountingCapability.Customers));
        Assert.True(provider.Supports(AccountingCapability.Customers | AccountingCapability.Products));
        Assert.False(provider.Supports(AccountingCapability.Customers | AccountingCapability.Sales));
    }

    [Fact]
    public void Declared_flag_without_implemented_port_is_rejected()
    {
        IAccountingProvider provider = new DeclaredProvider { SupportedCapabilities = AccountingCapability.Customers };
        Assert.Throws<CapabilityNotSupportedException>(
            () => provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers));
    }

    [Fact]
    public void Wrong_port_or_combined_flags_are_rejected()
    {
        IAccountingProvider provider = new DeclaredProvider
        {
            SupportedCapabilities = AccountingCapability.Customers | AccountingCapability.Products
        };
        Assert.Throws<CapabilityNotSupportedException>(
            () => provider.RequireCapability<IProductSource>(AccountingCapability.Customers));
        Assert.Throws<CapabilityNotSupportedException>(
            () => provider.RequireCapability<ICustomerSource>(
                AccountingCapability.Customers | AccountingCapability.Products));
    }

    [Fact]
    public void Null_provider_is_rejected()
    {
        IAccountingProvider provider = null!;
        Assert.Throws<ArgumentNullException>(() => provider.Supports(AccountingCapability.Customers));
        Assert.Throws<ArgumentNullException>(
            () => provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers));
    }
    private sealed class GenericCustomersProvider : IAccountingProvider, ISyncablePort<SourceCustomer>
    {
        public AccountingCapability SupportedCapabilities => AccountingCapability.Customers;
        public string CapabilityName => "Customers";
        public FaraTaraz.Modules.Ingestion.Domain.Synchronization.SyncModeSupport SyncModeSupport
            => throw new NotSupportedException();
        public Task<FaraTaraz.Modules.Ingestion.Domain.Synchronization.SyncBatch<SourceCustomer>> SyncAsync(
            FaraTaraz.Modules.Ingestion.Domain.Synchronization.SyncRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [Fact]
    public void Generic_syncable_port_resolves_when_declared_and_implemented()
    {
        IAccountingProvider provider = new GenericCustomersProvider();
        var port = provider.RequireCapability<ISyncablePort<SourceCustomer>>(AccountingCapability.Customers);
        Assert.Same(provider, port);
    }

    [Fact]
    public void Generic_syncable_port_rejects_unsupported_and_invalid_flags()
    {
        IAccountingProvider provider = new GenericCustomersProvider();
        Assert.Throws<CapabilityNotSupportedException>(
            () => provider.RequireCapability<ISyncablePort<SourceCustomer>>(AccountingCapability.Products));
        Assert.Throws<CapabilityNotSupportedException>(
            () => provider.RequireCapability<ISyncablePort<SourceCustomer>>(AccountingCapability.None));
        Assert.Throws<CapabilityNotSupportedException>(
            () => provider.RequireCapability<ISyncablePort<SourceCustomer>>((AccountingCapability)128));
    }

}

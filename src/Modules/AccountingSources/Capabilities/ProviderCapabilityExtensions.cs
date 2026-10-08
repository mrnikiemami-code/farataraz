namespace FaraTaraz.Modules.AccountingSources.Capabilities;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;

/// <summary>
/// Explicit capability resolution helpers.
///
/// Invariant: unsupported capability is an explicit failure, never an empty result.
/// </summary>
public static class ProviderCapabilityExtensions
{
    private const AccountingCapability KnownCapabilities =
        AccountingCapability.Customers | AccountingCapability.Products |
        AccountingCapability.Sales | AccountingCapability.Inventory |
        AccountingCapability.Purchases | AccountingCapability.Payments;

    /// <summary>True only for a non-empty set of declared, known capabilities.</summary>
    public static bool Supports(this IAccountingProvider provider, AccountingCapability capability)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return capability != AccountingCapability.None
            && (capability & ~KnownCapabilities) == AccountingCapability.None
            && (provider.SupportedCapabilities & capability) == capability;
    }

    /// <summary>
    /// Resolves a matching capability port. A mismatched requested port and capability
    /// must fail even if the provider happens to implement both interfaces.
    /// </summary>
    public static TCapability RequireCapability<TCapability>(
        this IAccountingProvider provider,
        AccountingCapability capability)
        where TCapability : class, ICapabilityPort
    {
        ArgumentNullException.ThrowIfNull(provider);
        var expected = typeof(TCapability) == typeof(ICustomerSource) ? AccountingCapability.Customers
            : typeof(TCapability) == typeof(IProductSource) ? AccountingCapability.Products
            : typeof(TCapability) == typeof(ISalesSource) ? AccountingCapability.Sales
            : typeof(TCapability) == typeof(IInventorySource) ? AccountingCapability.Inventory
            : typeof(TCapability) == typeof(IPurchaseSource) ? AccountingCapability.Purchases
            : typeof(TCapability) == typeof(IPaymentSource) ? AccountingCapability.Payments
            : typeof(TCapability) == typeof(ISyncablePort<SourceCustomer>) ? AccountingCapability.Customers
            : typeof(TCapability) == typeof(ISyncablePort<SourceProduct>) ? AccountingCapability.Products
            : typeof(TCapability) == typeof(ISyncablePort<SourceSalesRecord>) ? AccountingCapability.Sales
            : typeof(TCapability) == typeof(ISyncablePort<SourceInventoryRecord>) ? AccountingCapability.Inventory
            : typeof(TCapability) == typeof(ISyncablePort<SourcePurchaseRecord>) ? AccountingCapability.Purchases
            : typeof(TCapability) == typeof(ISyncablePort<SourcePaymentRecord>) ? AccountingCapability.Payments
            : AccountingCapability.None;

        if (expected == AccountingCapability.None || capability != expected
            || !provider.Supports(capability) || provider is not TCapability port)
            throw new CapabilityNotSupportedException(capability);

        return port;
    }
}

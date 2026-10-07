namespace FaraTaraz.Core.Accounting;

using FaraTaraz.BuildingBlocks.Accounting;

/// <summary>
/// Marker interface for a single capability port.
///
/// Capability ports are independent and explicit. A provider implements only the
/// ports it genuinely supports. Ports are platform abstractions: they are NOT
/// provider-specific, and they never expose provider DTOs or field names.
/// </summary>
public interface ICapabilityPort
{
}

/// <summary>Customers capability port.</summary>
public interface ICustomerSource : ICapabilityPort
{
    /// <summary>
    /// Returns customers for a specific accounting source.
    /// The return type is a provider-agnostic source record, never a provider DTO.
    /// (W0 seam only — the synchronization pipeline is not implemented in W0.)
    /// </summary>
    System.Collections.Generic.IReadOnlyList<SourceModel.SourceCustomer> ListCustomers(
        FaraTaraz.BuildingBlocks.Identifiers.AccountingSourceId sourceId);
}

/// <summary>Products capability port.</summary>
public interface IProductSource : ICapabilityPort
{
    System.Collections.Generic.IReadOnlyList<SourceModel.SourceProduct> ListProducts(
        FaraTaraz.BuildingBlocks.Identifiers.AccountingSourceId sourceId);
}

/// <summary>Sales capability port.</summary>
public interface ISalesSource : ICapabilityPort
{
}

/// <summary>Inventory capability port.</summary>
public interface IInventorySource : ICapabilityPort
{
}

/// <summary>Purchases capability port.</summary>
public interface IPurchaseSource : ICapabilityPort
{
}

/// <summary>Payments capability port.</summary>
public interface IPaymentSource : ICapabilityPort
{
}

/// <summary>
/// The accounting provider port.
///
/// A provider declares which capabilities it supports and exposes the matching
/// capability ports. It does NOT implement fake methods returning empty collections:
/// unsupported capabilities are handled explicitly via
/// <see cref="ProviderCapabilityExtensions.RequireCapability{TCapability}(IAccountingProvider, AccountingCapability)"/>.
/// </summary>
public interface IAccountingProvider
{
    AccountingCapability SupportedCapabilities { get; }
}

/// <summary>
/// Explicit capability resolution helpers.
///
/// Invariant: unsupported capability is an explicit failure, never an empty result.
/// </summary>
public static class ProviderCapabilityExtensions
{
    /// <summary>True when the provider declares the capability supported.</summary>
    public static bool Supports(this IAccountingProvider provider, AccountingCapability capability)
        => provider.SupportedCapabilities.HasFlag(capability);

    /// <summary>
    /// Returns the capability port only if the provider genuinely supports it.
    /// Throws <see cref="CapabilityNotSupportedException"/> otherwise.
    /// Never returns null or an empty collection for an unsupported capability.
    /// </summary>
    public static TCapability RequireCapability<TCapability>(
        this IAccountingProvider provider,
        AccountingCapability capability)
        where TCapability : class, ICapabilityPort
    {
        if (!provider.SupportedCapabilities.HasFlag(capability))
        {
            throw new CapabilityNotSupportedException(capability);
        }

        return provider is TCapability port
            ? port
            : throw new CapabilityNotSupportedException(capability);
    }
}

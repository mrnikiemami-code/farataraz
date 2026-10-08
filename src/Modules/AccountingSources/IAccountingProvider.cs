namespace FaraTaraz.Modules.AccountingSources;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;

/// <summary>
/// Marker interface for a single capability port.
///
/// Capability ports are independent and explicit. A provider implements only the
/// ports it genuinely supports. Ports are platform abstractions: they are NOT
/// provider-specific, and they never expose provider DTOs or field names.
///
/// Capability-specific metadata (<c>CapabilityName</c>, <c>SyncModeSupport</c>) lives on
/// <see cref="ISyncablePort{TRecord}"/> rather than here, so two capabilities can declare
/// independent values without a shared-member conflict.
/// </summary>
public interface ICapabilityPort
{
}

/// <summary>
/// A capability port that can be synchronized in bounded, resumable pages.
///
/// The contract is batch-oriented <c>Task&lt;SyncBatch&lt;TRecord&gt;&gt;</c> rather than
/// <c>IAsyncEnumerable&lt;&gt;</c> or <c>Task&lt;IReadOnlyList&lt;T&gt;&gt;</c> on purpose:
/// it preserves explicit checkpoint/restart semantics (a cursor per page), keeps each
/// page bounded in memory, and makes cancellation explicit.
/// </summary>
public interface ISyncablePort<TRecord> : ICapabilityPort
{
    /// <summary>
    /// Stable, capability-level identifier (e.g. "Customers", "Products"). Used to scope
    /// cursors and to validate resume; it is a platform namespace, never a provider value.
    /// </summary>
    string CapabilityName { get; }

    /// <summary>Declares which synchronization modes this capability supports.</summary>
    SyncModeSupport SyncModeSupport { get; }

    /// <summary>
    /// Synchronizes one bounded page for the requested source.
    ///
    /// - Propagates <paramref name="cancellationToken"/>; cancellation throws
    ///   <c>OperationCanceledException</c> and is never classified as a provider failure.
    /// - Throws <see cref="SyncModeNotSupportedException"/> if an unsupported mode is
    ///   requested; it never silently falls back.
    /// - Throws <see cref="InvalidSyncCursorException"/> /
    ///   <see cref="SyncCursorScopeMismatchException"/> on an invalid/mismatched resume.
    /// - Never returns null; an unsupported capability is expressed by the provider not
    ///   implementing this port at all (see <see cref="ProviderCapabilityExtensions"/>).
    /// </summary>
    Task<SyncBatch<TRecord>> SyncAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Customers capability port.</summary>
public interface ICustomerSource : ISyncablePort<SourceCustomer>
{
}

/// <summary>Products capability port.</summary>
public interface IProductSource : ISyncablePort<SourceProduct>
{
}

/// <summary>Sales capability port.</summary>
public interface ISalesSource : ISyncablePort<SourceSalesRecord>
{
}

/// <summary>Inventory capability port.</summary>
public interface IInventorySource : ISyncablePort<SourceInventoryRecord>
{
}

/// <summary>Purchases capability port.</summary>
public interface IPurchaseSource : ISyncablePort<SourcePurchaseRecord>
{
}

/// <summary>Payments capability port.</summary>
public interface IPaymentSource : ISyncablePort<SourcePaymentRecord>
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

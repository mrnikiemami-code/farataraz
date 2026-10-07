namespace FaraTaraz.Core.Accounting;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Core.Synchronization;

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
public interface ICustomerSource : ISyncablePort<SourceModel.SourceCustomer>
{
}

/// <summary>Products capability port.</summary>
public interface IProductSource : ISyncablePort<SourceModel.SourceProduct>
{
}

/// <summary>Sales capability port.</summary>
public interface ISalesSource : ISyncablePort<SourceModel.SourceSalesRecord>
{
}

/// <summary>Inventory capability port.</summary>
public interface IInventorySource : ISyncablePort<SourceModel.SourceInventoryRecord>
{
}

/// <summary>Purchases capability port.</summary>
public interface IPurchaseSource : ISyncablePort<SourceModel.SourcePurchaseRecord>
{
}

/// <summary>Payments capability port.</summary>
public interface IPaymentSource : ISyncablePort<SourceModel.SourcePaymentRecord>
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

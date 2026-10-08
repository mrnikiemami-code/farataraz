namespace FaraTaraz.Adapters.Accounting.Mock;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Deterministic Mock accounting provider used to prove the synchronization contract (W1).
///
/// It supports Customers + Products only (NOT Inventory/Sales/Purchases/Payments), so
/// unsupported capabilities still fail explicitly via <c>RequireCapability</c>. Each
/// capability is backed by a <see cref="MockCapabilitySync{TRecord}"/> configured with a
/// deterministic scenario. Provider-specific concepts stay inside this namespace; the
/// platform core references only neutral Core types.
///
/// The two capability ports each declare their own <c>CapabilityName</c>/<c>SyncModeSupport</c>
/// via their closed <c>ISyncablePort&lt;*&gt;</c> interface, so explicit interface
/// implementation keeps each port's values independent.
/// </summary>
public sealed class MockAccountingProvider : IAccountingProvider, ICustomerSource, IProductSource
{
    public const string CustomersCapability = "Customers";
    public const string ProductsCapability = "Products";

    public AccountingCapability SupportedCapabilities { get; }

    private readonly MockCapabilitySync<SourceCustomer> _customers;
    private readonly MockCapabilitySync<SourceProduct> _products;

    public MockAccountingProvider(
        MockCapabilitySync<SourceCustomer> customers,
        MockCapabilitySync<SourceProduct> products)
    {
        _customers = customers;
        _products = products;
        SupportedCapabilities = AccountingCapability.Customers | AccountingCapability.Products;
    }

    // ISyncablePort<SourceCustomer>
    string ISyncablePort<SourceCustomer>.CapabilityName => CustomersCapability;
    SyncModeSupport ISyncablePort<SourceCustomer>.SyncModeSupport => _customers.ModeSupport;

    Task<SyncBatch<SourceCustomer>> ISyncablePort<SourceCustomer>.SyncAsync(
        SyncRequest request,
        CancellationToken cancellationToken)
        => _customers.SyncAsync(request, cancellationToken);

    // ISyncablePort<SourceProduct>
    string ISyncablePort<SourceProduct>.CapabilityName => ProductsCapability;
    SyncModeSupport ISyncablePort<SourceProduct>.SyncModeSupport => _products.ModeSupport;

    Task<SyncBatch<SourceProduct>> ISyncablePort<SourceProduct>.SyncAsync(
        SyncRequest request,
        CancellationToken cancellationToken)
        => _products.SyncAsync(request, cancellationToken);
}

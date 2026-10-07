namespace FaraTaraz.Adapters.Accounting.Mock;

using System.Collections.Generic;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Core.Accounting;
using FaraTaraz.Core.SourceModel;

/// <summary>
/// Mock customer capability. Demonstrates a PARTIAL capability set: this provider
/// supports Customers and Products but NOT Inventory/Sales/Purchases/Payments.
///
/// External codes are source-scoped. Two sources may share or differ on codes;
/// equal codes do NOT imply equal canonical entities (see architecture ADR-004).
/// </summary>
public sealed class MockCustomerSource : ICustomerSource
{
    private readonly IReadOnlyList<SourceCustomer> _customers;

    public MockCustomerSource(IReadOnlyList<SourceCustomer> customers)
    {
        _customers = customers;
    }

    public IReadOnlyList<SourceCustomer> ListCustomers(AccountingSourceId sourceId)
        => _customers;
}

/// <summary>Mock product capability. See <see cref="MockCustomerSource"/> for the contract.</summary>
public sealed class MockProductSource : IProductSource
{
    private readonly IReadOnlyList<SourceProduct> _products;

    public MockProductSource(IReadOnlyList<SourceProduct> products)
    {
        _products = products;
    }

    public IReadOnlyList<SourceProduct> ListProducts(AccountingSourceId sourceId)
        => _products;
}

/// <summary>
/// Mock accounting provider. Supports Customers + Products only.
///
/// This adapter proves the capability contract end-to-end:
/// - SupportedCapabilities = Customers | Products (Inventory is NOT declared).
/// - It implements ICustomerSource / IProductSource directly, so the matching
///   capability ports resolve; IInventorySource is NOT implemented, so
///   RequireCapability&lt;IInventorySource&gt; fails explicitly.
/// - Provider-specific concepts stay inside this namespace; the platform core
///   references only neutral Core types.
/// </summary>
public sealed class MockAccountingProvider : IAccountingProvider, ICustomerSource, IProductSource
{
    public AccountingCapability SupportedCapabilities { get; }

    private readonly ICustomerSource _customers;
    private readonly IProductSource _products;

    public MockAccountingProvider(
        AccountingCapability supportedCapabilities,
        ICustomerSource customers,
        IProductSource products)
    {
        SupportedCapabilities = supportedCapabilities;
        _customers = customers;
        _products = products;
    }

    // ICustomerSource — resolved via RequireCapability<ICustomerSource>.
    public IReadOnlyList<SourceCustomer> ListCustomers(AccountingSourceId sourceId)
        => _customers.ListCustomers(sourceId);

    // IProductSource — resolved via RequireCapability<IProductSource>.
    public IReadOnlyList<SourceProduct> ListProducts(AccountingSourceId sourceId)
        => _products.ListProducts(sourceId);
}

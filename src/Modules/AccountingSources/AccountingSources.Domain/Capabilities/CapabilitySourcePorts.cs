namespace FaraTaraz.Modules.AccountingSources.Capabilities;

using FaraTaraz.Modules.Ingestion.Domain.SourceModel;

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


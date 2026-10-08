namespace FaraTaraz.SyncContracts.Tests;

using System.Collections.Generic;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Deterministic Mock providers for the conformance harness and contract tests.
///
/// The Mock is a TEST provider that proves the synchronization contract across controlled,
/// repeatable scenarios. It is deterministic — no random data — so every test is stable.
/// This helper is the ONLY place the Mock is wired; the conformance harness and contract
/// tests observe only the neutral public contract surface.
/// </summary>
public static class MockSyncScenarios
{
    public static MockAccountingProvider CustomersAndProducts(
        IReadOnlyList<SourceCustomer> customers,
        IReadOnlyList<SourceProduct> products)
        => CustomersAndProducts(
            customers,
            products,
            SyncModeSupport.FullAndIncremental,
            SyncModeSupport.FullOnly);

    public static MockAccountingProvider CustomersAndProducts(
        IReadOnlyList<SourceCustomer> customers,
        IReadOnlyList<SourceProduct> products,
        SyncModeSupport customerMode,
        SyncModeSupport productMode,
        int customerPageSize = 2,
        int productPageSize = 2,
        int customerTransientFailures = 0,
        int productTransientFailures = 0)
        => new(
            new MockCapabilitySync<SourceCustomer>(
                MockAccountingProvider.CustomersCapability,
                customers,
                customerMode,
                customerPageSize,
                customerTransientFailures),
            new MockCapabilitySync<SourceProduct>(
                MockAccountingProvider.ProductsCapability,
                products,
                productMode,
                productPageSize,
                productTransientFailures));

    /// <summary>A customers scenario: 5 customers, page size 2, full + incremental.</summary>
    public static SyncConformanceScenario<SourceCustomer> Customers5()
        => new(
            SourceIds.Source,
            MockAccountingProvider.CustomersCapability,
            SyncModeSupport.FullAndIncremental,
            new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
                MockSources.Customer(SourceIds.Source, "C3"),
                MockSources.Customer(SourceIds.Source, "C4"),
                MockSources.Customer(SourceIds.Source, "C5"),
            },
            PageSize: 2);

    /// <summary>A products scenario: 4 products, page size 2, full only.</summary>
    public static SyncConformanceScenario<SourceProduct> Products4()
        => new(
            SourceIds.Source,
            MockAccountingProvider.ProductsCapability,
            SyncModeSupport.FullOnly,
            new[]
            {
                MockSources.Product(SourceIds.Source, "P1"),
                MockSources.Product(SourceIds.Source, "P2"),
                MockSources.Product(SourceIds.Source, "P3"),
                MockSources.Product(SourceIds.Source, "P4"),
            },
            PageSize: 2);

    /// <summary>A 5-customer provider (page size 2), used for resume/interruption tests.</summary>
    public static MockAccountingProvider ProviderWith5Customers()
        => CustomersAndProducts(
            new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
                MockSources.Customer(SourceIds.Source, "C3"),
                MockSources.Customer(SourceIds.Source, "C4"),
                MockSources.Customer(SourceIds.Source, "C5"),
            },
            Array.Empty<SourceProduct>());
}

/// <summary>Shared deterministic identifiers for contract tests.</summary>
public static class SourceIds
{
    public static readonly FaraTaraz.BuildingBlocks.Identifiers.AccountingSourceId Source =
        new("src-mock-1");
}

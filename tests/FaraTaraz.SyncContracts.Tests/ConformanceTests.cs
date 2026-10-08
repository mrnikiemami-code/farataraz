namespace FaraTaraz.SyncContracts.Tests;

using System.Collections.Generic;
using System.Threading.Tasks;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Xunit;

/// <summary>
/// Exercises the reusable conformance harness (<see cref="SyncConformance"/>) against the
/// Mock provider for BOTH capabilities. This proves the contract end-to-end and demonstrates
/// that the harness is adapter-agnostic: any future adapter (Asan, Sepidar, …) can be tested
/// against the SAME behavioral contract by supplying its own scenario.
/// </summary>
public class ConformanceTests
{
    [Fact]
    public async Task Customers_full_sync_is_bounded_contiguous_and_complete()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
                MockSources.Customer(SourceIds.Source, "C3"),
                MockSources.Customer(SourceIds.Source, "C4"),
                MockSources.Customer(SourceIds.Source, "C5"),
            },
            products: Array.Empty<SourceProduct>());

        await SyncConformance.FullSyncReturnsBoundedPages<SourceCustomer>(
            provider, AccountingCapability.Customers, MockSyncScenarios.Customers5());
    }

    [Fact]
    public async Task Products_full_sync_is_bounded_contiguous_and_complete()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: Array.Empty<SourceCustomer>(),
            products: new[]
            {
                MockSources.Product(SourceIds.Source, "P1"),
                MockSources.Product(SourceIds.Source, "P2"),
                MockSources.Product(SourceIds.Source, "P3"),
                MockSources.Product(SourceIds.Source, "P4"),
            });

        await SyncConformance.FullSyncReturnsBoundedPages<SourceProduct>(
            provider, AccountingCapability.Products, MockSyncScenarios.Products4());
    }

    [Fact]
    public async Task Customers_incremental_is_supported()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
            },
            products: Array.Empty<SourceProduct>());

        await SyncConformance.IncrementalBehaviorMatchesDeclaration<SourceCustomer>(
            provider, AccountingCapability.Customers, MockSyncScenarios.Customers5());
    }

    [Fact]
    public async Task Products_incremental_is_declared_unsupported_and_fails_explicitly()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: Array.Empty<SourceCustomer>(),
            products: new[]
            {
                MockSources.Product(SourceIds.Source, "P1"),
                MockSources.Product(SourceIds.Source, "P2"),
            });

        // Products is Full-only; requesting Incremental must fail explicitly, never fall back.
        await SyncConformance.IncrementalBehaviorMatchesDeclaration<SourceProduct>(
            provider, AccountingCapability.Products, MockSyncScenarios.Products4());
    }

    [Fact]
    public async Task Customers_cancellation_propagates_and_is_not_a_provider_failure()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
                MockSources.Customer(SourceIds.Source, "C3"),
            },
            products: Array.Empty<SourceProduct>());

        await SyncConformance.CancellationPropagatesAndIsNotAProviderFailure<SourceCustomer>(
            provider, AccountingCapability.Customers, MockSyncScenarios.Customers5());
    }

    [Fact]
    public async Task Customers_batch_size_is_honored()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
                MockSources.Customer(SourceIds.Source, "C3"),
            },
            products: Array.Empty<SourceProduct>());

        await SyncConformance.BatchSizeIsHonored<SourceCustomer>(
            provider, AccountingCapability.Customers, MockSyncScenarios.Customers5());
    }

    [Fact]
    public async Task Customers_cursor_continuation_works()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
                MockSources.Customer(SourceIds.Source, "C3"),
                MockSources.Customer(SourceIds.Source, "C4"),
            },
            products: Array.Empty<SourceProduct>());

        await SyncConformance.CursorContinuationWorks<SourceCustomer>(
            provider, AccountingCapability.Customers, MockSyncScenarios.Customers5());
    }

    [Fact]
    public async Task Customers_valid_empty_source_returns_empty_completion()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: Array.Empty<SourceCustomer>(),
            products: Array.Empty<SourceProduct>());

        // Scenario describes an empty dataset for a declared capability.
        var emptyScenario = new SyncConformanceScenario<SourceCustomer>(
            SourceIds.Source,
            MockAccountingProvider.CustomersCapability,
            SyncModeSupport.FullAndIncremental,
            Array.Empty<SourceCustomer>());

        await SyncConformance.ValidEmptySourceReturnsEmptyCompletion<SourceCustomer>(
            provider, AccountingCapability.Customers, emptyScenario);
    }

    [Fact]
    public void Unsupported_capability_fails_explicitly_not_empty()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: Array.Empty<SourceCustomer>(),
            products: Array.Empty<SourceProduct>());

        // Inventory is NOT declared by the Mock provider.
        SyncConformance.UnsupportedCapabilityFailsExplicitly<SourceCustomer>(
            provider, AccountingCapability.Inventory);
    }
}

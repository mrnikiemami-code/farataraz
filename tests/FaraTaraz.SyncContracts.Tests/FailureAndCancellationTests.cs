namespace FaraTaraz.SyncContracts.Tests;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Core.Accounting;
using FaraTaraz.Core.SourceModel;
using FaraTaraz.Core.Synchronization;
using Xunit;

/// <summary>
/// Failure taxonomy and cancellation evidence (task §14, §15): transient failure is
/// classifiable and retryable; unsupported mode fails explicitly; cancellation propagates
/// and is NEVER a provider failure.
/// </summary>
public class FailureAndCancellationTests
{
    [Fact]
    public async Task TransientFailure_IsClassifiedAndRetriesToSuccess()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
            },
            products: Array.Empty<SourceProduct>(),
            customerMode: SyncModeSupport.FullAndIncremental,
            productMode: SyncModeSupport.FullOnly,
            customerTransientFailures: 2);

        var port = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);

        // First two attempts fail transiently (retryable), then succeed.
        for (var i = 0; i < 2; i++)
        {
            var ex = await Assert.ThrowsAsync<SyncTransientException>(
                () => port.SyncAsync(new SyncRequest(SourceIds.Source, SyncMode.Full)));

            Assert.Equal(SyncFailureCategory.Transient, ex.Category);
        }

        // After the transient failures are exhausted, the call succeeds.
        var batch = await port.SyncAsync(new SyncRequest(SourceIds.Source, SyncMode.Full));
        Assert.Equal(2, batch.Records.Count);
    }

    [Fact]
    public async Task Cancellation_PropagatesAndIsNotAProviderFailure()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
            },
            products: Array.Empty<SourceProduct>());

        var port = provider.RequireCapability<ICustomerSource>(AccountingCapability.Customers);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAsync<OperationCanceledException>(
            () => port.SyncAsync(
                new SyncRequest(SourceIds.Source, SyncMode.Full), cts.Token));

        // Cancellation is a caller action, never a provider failure classification.
        Assert.IsNotType<SyncProviderException>(ex);
    }

    [Fact]
    public async Task UnsupportedIncrementalMode_FailsExplicitly()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: Array.Empty<SourceCustomer>(),
            products: new[]
            {
                MockSources.Product(SourceIds.Source, "P1"),
                MockSources.Product(SourceIds.Source, "P2"),
            },
            customerMode: SyncModeSupport.FullAndIncremental,
            productMode: SyncModeSupport.FullOnly);

        var port = provider.RequireCapability<IProductSource>(AccountingCapability.Products);

        var ex = await Assert.ThrowsAsync<SyncModeNotSupportedException>(
            () => port.SyncAsync(
                new SyncRequest(SourceIds.Source, SyncMode.Incremental)));

        Assert.Equal(SyncFailureCategory.Permanent, ex.Category);
        Assert.Equal(SyncMode.Incremental, ex.Mode);
    }
}

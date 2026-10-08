namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Xunit;

/// <summary>
/// CQRS direction proof (task §27).
///
/// Proves the W1-R1 CQRS structure end to end:
///   delivery boundary (ISender) → Application query → Application handler → capability port.
///
/// The use case is dispatched through <c>ISender</c> (the delivery boundary, never
/// <c>IMediator</c>), delegates to the provider-independent capability port
/// (<c>ISyncablePort&lt;SourceCustomer&gt;</c>), and enforces the tenant-authority invariant by
/// asserting the Tenant context is trusted. The Mock is the ONLY provider wiring; the test
/// observes only the neutral contract surface.
/// </summary>
public class CQRSProofTests
{
    private static readonly AccountingSourceId SourceId = new("src-mock-1");
    private static readonly TenantId TenantId = new("tenant-proof-1");

    /// <summary>A deterministic 5-customer Mock provider (page size 2, full + incremental).</summary>
    private static MockAccountingProvider ProviderWith5Customers() => new(
        new MockCapabilitySync<SourceCustomer>(
            MockAccountingProvider.CustomersCapability,
            new[]
            {
                MockSources.Customer(SourceId, "C1"),
                MockSources.Customer(SourceId, "C2"),
                MockSources.Customer(SourceId, "C3"),
                MockSources.Customer(SourceId, "C4"),
                MockSources.Customer(SourceId, "C5")
            },
            SyncModeSupport.FullAndIncremental),
        new MockCapabilitySync<SourceProduct>(
            MockAccountingProvider.ProductsCapability,
            Array.Empty<SourceProduct>(),
            SyncModeSupport.FullOnly));

    private static ISender BuildSender(MockAccountingProvider provider)
    {
        var services = new ServiceCollection();

        // Register every Application handler in the Ingestion.Application assembly.
        // MediatR 12.5.0 exposes only the Action<MediatRServiceConfiguration> surface;
        // the handler assembly is scanned for IRequestHandler<> implementations.
        services.AddMediatR(
            config => config.RegisterServicesFromAssembly(
                typeof(SynchronizeCustomersHandler).Assembly));

        // The provider implements the capability port; the use case depends only on it.
        services.AddScoped<ISyncablePort<SourceCustomer>>(_ => provider);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<ISender>();
    }

    [Fact]
    public async Task ISender_dispatches_the_use_case_and_delegates_to_the_capability_port()
    {
        var provider = ProviderWith5Customers();
        var sender = BuildSender(provider);

        var query = new SynchronizeCustomersQuery(
            TenantContext.FromAuthenticatedPrincipal(TenantId),
            new SyncRequest(SourceId, SyncMode.Full));

        var result = await sender.Send(query);

        // The bounded-page cursor contract is walked to completion by the Application handler.
        Assert.Equal(
            new[] { "C1", "C2", "C3", "C4", "C5" },
            result.Select(c => c.Code));
    }

    [Fact]
    public async Task Untrusted_Tenant_context_is_rejected_by_the_use_case()
    {
        var provider = ProviderWith5Customers();
        var sender = BuildSender(provider);

        var query = new SynchronizeCustomersQuery(
            new TenantContext(TenantId, TenantContextOrigin.ClientInput),
            new SyncRequest(SourceId, SyncMode.Full));

        await Assert.ThrowsAsync<UnauthorizedTenantException>(() => sender.Send(query));
    }
}

namespace FaraTaraz.Infrastructure.IntegrationTests;

using System.Linq;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Composition;
using FaraTaraz.Modules.Ingestion.Infrastructure.Composition;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Regression guard for module-owned composition: the trusted tenant scope is registered
/// once, regardless of module order, and absent/untrusted contexts fail closed.
/// </summary>
public sealed class TenantScopeCompositionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Both_module_orders_register_one_trusted_scope(bool ingestionFirst)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => TenantContext.FromAuthenticatedPrincipal(new TenantId("tenant-a")));
        RegisterModules(services, ingestionFirst);

        Assert.Single(services.Where(x => x.ServiceType == typeof(DatabaseTenantScope)));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolved = scope.ServiceProvider.GetRequiredService<DatabaseTenantScope>();
        Assert.True(resolved.IsTrusted);
        Assert.Equal("tenant-a", resolved.TenantId?.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Missing_context_fails_closed_in_both_module_orders(bool ingestionFirst)
    {
        var services = new ServiceCollection();
        RegisterModules(services, ingestionFirst);
        Assert.Single(services.Where(x => x.ServiceType == typeof(DatabaseTenantScope)));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<DatabaseTenantScope>().IsTrusted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Client_input_context_fails_closed_in_both_module_orders(bool ingestionFirst)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new TenantContext(new TenantId("spoofed"), TenantContextOrigin.ClientInput));
        RegisterModules(services, ingestionFirst);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<DatabaseTenantScope>().IsTrusted);
    }

    private static void RegisterModules(IServiceCollection services, bool ingestionFirst)
    {
        const string connection = "Host=localhost;Port=54320;Username=postgres;Password=unused;Database=farataraz";
        if (ingestionFirst)
        {
            services.AddIngestionInfrastructure(connection);
            services.AddAccountingSourcesInfrastructure(connection);
        }
        else
        {
            services.AddAccountingSourcesInfrastructure(connection);
            services.AddIngestionInfrastructure(connection);
        }
    }
}

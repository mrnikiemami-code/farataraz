namespace FaraTaraz.Infrastructure.IntegrationTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Authorization;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Real PostgreSQL tests for <see cref="EfAccountingSourceProviderResolver"/> (task M5): the
/// authoritative provider identity comes from the persisted AccountingSource configuration, scoped
/// to the trusted tenant, and fails closed for unknown/cross-tenant sources, blank configuration
/// and absent trusted scope.
/// </summary>
public sealed class AccountingSourceProviderResolverTests
{
    private static TenantId Tenant(string value) => new(value);
    private static AccountingSourceId Source(string value) => new(value);

    private static DatabaseTenantScope Trusted(string tenant)
        => DatabaseTenantScope.FromTrusted(TenantContext.FromAuthenticatedPrincipal(Tenant(tenant)));

    private static async Task SeedAsync(IntegrationTestDb db, string tenant, string source, string provider)
    {
        await using var seed = db.AccountingSources();
        seed.Tenants.Add(new TenantEntity { Id = tenant, Name = "T " + tenant });
        await seed.SaveChangesAsync();
        var t = await seed.Tenants.FirstAsync(x => x.Id == tenant);
        seed.AccountingSources.Add(new AccountingSourceEntity
        {
            Id = source,
            TenantId = tenant,
            Provider = provider,
            DisplayName = "S " + source,
            Status = 0,
            Tenant = t,
        });
        await seed.SaveChangesAsync();
    }

    [Fact]
    public async Task Resolves_the_configured_provider_for_the_trusted_tenant()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        await SeedAsync(db, tenant, source, "Sepidar");

        await using var ctx = db.AccountingSources();
        var resolver = new EfAccountingSourceProviderResolver(ctx, Trusted(tenant));

        var provider = await resolver.ResolveProviderAsync(Tenant(tenant), Source(source));

        Assert.NotNull(provider);
        Assert.Equal("Sepidar", provider!.Value.Value);
    }

    [Fact]
    public async Task Unknown_source_resolves_to_null()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        await SeedAsync(db, tenant, Guid.NewGuid().ToString("N"), "Mock");

        await using var ctx = db.AccountingSources();
        var resolver = new EfAccountingSourceProviderResolver(ctx, Trusted(tenant));

        Assert.Null(await resolver.ResolveProviderAsync(Tenant(tenant), Source(Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public async Task Cross_tenant_source_resolves_to_null()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var owner = Guid.NewGuid().ToString("N");
        var other = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        await SeedAsync(db, owner, source, "Mock");

        await using var ctx = db.AccountingSources();
        // Trusted scope is a DIFFERENT tenant; the caller tenant argument is ignored.
        var resolver = new EfAccountingSourceProviderResolver(ctx, Trusted(other));

        Assert.Null(await resolver.ResolveProviderAsync(Tenant(owner), Source(source)));
    }

    [Fact]
    public async Task Blank_provider_configuration_resolves_to_null()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        await SeedAsync(db, tenant, source, "   ");

        await using var ctx = db.AccountingSources();
        var resolver = new EfAccountingSourceProviderResolver(ctx, Trusted(tenant));

        Assert.Null(await resolver.ResolveProviderAsync(Tenant(tenant), Source(source)));
    }

    [Fact]
    public async Task Cancellation_during_resolution_propagates()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        await SeedAsync(db, tenant, source, "Mock");

        await using var ctx = db.AccountingSources();
        var resolver = new EfAccountingSourceProviderResolver(ctx, Trusted(tenant));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => resolver.ResolveProviderAsync(Tenant(tenant), Source(source), cts.Token));
    }

    [Fact]
    public async Task Missing_trusted_scope_resolves_to_null()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        await SeedAsync(db, tenant, source, "Mock");

        await using var ctx = db.AccountingSources();
        var resolver = new EfAccountingSourceProviderResolver(ctx, DatabaseTenantScope.None);

        Assert.Null(await resolver.ResolveProviderAsync(Tenant(tenant), Source(source)));
    }
}

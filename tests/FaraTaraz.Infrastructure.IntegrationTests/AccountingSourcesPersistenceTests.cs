namespace FaraTaraz.Infrastructure.IntegrationTests;

using System.Reflection;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Authorization;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Real PostgreSQL integration tests for the AccountingSources persistence layer: tenant
/// isolation and fail-closed source ownership (ADR-010 decisions 4, 6). These exercise the
/// actual <c>EfAccountingSourceOwnership</c> oracle against a real database, never the InMemory
/// provider. Each test uses unique tenant/source ids so the shared database never collides.
/// </summary>
public sealed class AccountingSourcesPersistenceTests
{
    private static TenantId Tenant(string value) => new(value);
    private static AccountingSourceId Source(string value) => new(value);

    /// <summary>
    /// Establishes a trusted scope for the given tenant via a trusted execution context
    /// (<c>AuthenticatedPrincipal</c>). This mirrors what the trusted execution boundary does in
    /// production: the scope is created only from a trusted context, never from arbitrary input
    /// (ADR-010 decision 5).
    /// </summary>
    private static DatabaseTenantScope TrustedTenant(TenantId tenantId)
        => DatabaseTenantScope.FromTrusted(TenantContext.FromAuthenticatedPrincipal(tenantId));

    private static async Task SeedTenantSourceAsync(IntegrationTestDb db, TenantId tenantId, AccountingSourceId sourceId)
    {
        await using var seed = db.AccountingSources();
        seed.Tenants.Add(new TenantEntity { Id = tenantId.Value, Name = $"Tenant {tenantId.Value}" });
        await seed.SaveChangesAsync();

        var tenant = await seed.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId.Value);
        seed.AccountingSources.Add(new AccountingSourceEntity
        {
            Id = sourceId.Value,
            TenantId = tenantId.Value,
            Provider = "Mock",
            DisplayName = $"Source {sourceId.Value}",
            Status = 0,
            Tenant = tenant!,
        });
        await seed.SaveChangesAsync();
    }

    [Fact]
    public async Task Ownership_oracle_authorizes_source_for_own_trusted_tenant()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenantId = Guid.NewGuid().ToString("N");
        var sourceId = Guid.NewGuid().ToString("N");
        await SeedTenantSourceAsync(db, Tenant(tenantId), Source(sourceId));

        await using var ctx = db.AccountingSources();
        var oracle = new EfAccountingSourceOwnership(ctx, TrustedTenant(Tenant(tenantId)));

        var owned = await oracle.IsOwnedByAsync(Tenant(tenantId), Source(sourceId));

        Assert.True(owned);
    }

    [Fact]
    public async Task Ownership_oracle_denies_source_for_different_trusted_tenant()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var ownerTenant = Guid.NewGuid().ToString("N");
        var otherTenant = Guid.NewGuid().ToString("N");
        var sourceId = Guid.NewGuid().ToString("N");
        await SeedTenantSourceAsync(db, Tenant(ownerTenant), Source(sourceId));

        // The trusted tenant is the other tenant; the source exists only under ownerTenant.
        // Cross-tenant ownership is denied: the scoped query is parameterized by the trusted
        // tenant, so it never sees the other tenant's row (ADR-010 decision 4, Constitution A.3).
        await using var ctx = db.AccountingSources();
        var oracle = new EfAccountingSourceOwnership(ctx, TrustedTenant(Tenant(otherTenant)));

        var owned = await oracle.IsOwnedByAsync(Tenant(ownerTenant), Source(sourceId));

        Assert.False(owned);
    }

    [Fact]
    public async Task Ownership_oracle_fails_closed_without_trusted_scope()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenantId = Guid.NewGuid().ToString("N");
        var sourceId = Guid.NewGuid().ToString("N");
        await SeedTenantSourceAsync(db, Tenant(tenantId), Source(sourceId));

        await using var ctx = db.AccountingSources();
        // No trusted context bound -> fail-closed: never authorizes, even for a real source.
        var oracle = new EfAccountingSourceOwnership(ctx, DatabaseTenantScope.None);

        var owned = await oracle.IsOwnedByAsync(Tenant(tenantId), Source(sourceId));

        Assert.False(owned);
    }

    [Fact]
    public async Task Unique_constraint_rejects_duplicate_source_for_same_tenant()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenantId = Guid.NewGuid().ToString("N");
        var sourceId = Guid.NewGuid().ToString("N");
        await SeedTenantSourceAsync(db, Tenant(tenantId), Source(sourceId));

        // A second source with the SAME (tenant, id) violates the (TenantId, Id) unique constraint
        // at the database — cross-tenant isolation is enforced structurally (ADR-010 decision 4).
        await using var ctx = db.AccountingSources();
        ctx.AccountingSources.Add(new AccountingSourceEntity
        {
            Id = sourceId,
            TenantId = tenantId,
            Provider = "Mock",
            DisplayName = "Duplicate",
            Status = 0,
        });

        await Assert.ThrowsAnyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => ctx.SaveChangesAsync());
    }
}

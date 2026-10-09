namespace FaraTaraz.Infrastructure.IntegrationTests;

using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Authorization;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Smoke test: proves the integration fixture can connect to a real PostgreSQL database, apply
/// the module migrations, and query the migrated schema. All other persistence tests depend on
/// this succeeding.
/// </summary>
public sealed class IntegrationConnectionSmokeTests
{
    [Fact]
    public async Task Can_connect_and_apply_migrations()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        // Create a tenant and verify the migrated schema is writable and queryable. A unique
        // tenant id keeps this independent of other tests' data.
        var tenantId = Guid.NewGuid().ToString("N");
        await using var ctx = db.AccountingSources();
        ctx.Tenants.Add(new TenantEntity { Id = tenantId, Name = "Smoke" });
        await ctx.SaveChangesAsync();

        var found = await ctx.Tenants
            .AnyAsync(t => t.Id == tenantId);
        Assert.True(found);
    }
}

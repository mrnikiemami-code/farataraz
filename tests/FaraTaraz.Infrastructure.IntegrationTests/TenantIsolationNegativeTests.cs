namespace FaraTaraz.Infrastructure.IntegrationTests;

using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real PostgreSQL integration tests proving W2 tenant isolation (FT-W2-R1-TENANT-ISOLATION).
///
/// These are <b>negative</b> tests: they must fail against the original vulnerable code (which
/// trusted a caller-supplied <c>TenantId</c>) and pass after the repair (tenant authority comes
/// only from the trusted <see cref="DatabaseTenantScope"/>). Each uses unique tenant/source ids
/// so the shared database never collides. Every cross-tenant attempt is denied; same-tenant
/// operations still succeed.
/// </summary>
public sealed class TenantIsolationNegativeTests
{
    private static TenantId Tenant(string value) => new(value);
    private static AccountingSourceId Source(string value) => new(value);

    private static SourceRecordId Record(AccountingSourceId sourceId, string kind, string external)
        => new(sourceId, kind, external);

    private static SourceProvenance Provenance(SourceRecordId id, DateTime retrieved)
        => new(id, new BuildingBlocks.Accounting.ProviderId("Mock"), id.SourceId, retrieved);

    private static DatabaseTenantScope Scope(string tenant)
        => DatabaseTenantScope.FromTrusted(
            TenantContext.FromAuthenticatedPrincipal(Tenant(tenant)));

    private static async Task<int> CountRunsAsync(IntegrationTestDb db, string tenant, long runId)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SyncRuns.CountAsync(r => r.TenantId == tenant && r.Id == runId);
    }

    private static async Task<int> CountRecordsAsync(IntegrationTestDb db, TenantId tenant, AccountingSourceId source, string kind, string external)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SourceRecords.CountAsync(r =>
            r.TenantId == tenant.Value &&
            r.SourceId == source.Value &&
            r.RecordKind == kind &&
            r.ExternalId == external);
    }

    // --- 1. Tenant A cannot finish Tenant B's sync run -------------------------------------

    [Fact]
    public async Task Tenant_A_cannot_finish_Tenant_B_run()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenantA = Guid.NewGuid().ToString("N");
        var tenantB = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");

        // Tenant B starts a run and owns it.
        await using var ctxB = db.Ingestion();
        var runsB = new SyncRunRepository(ctxB, Scope(tenantB));
        var runB = await runsB.StartAsync(Tenant(tenantB), Source(source), "Customers", "Full");

        // Tenant A attempts to finish it -> denied: FinishAsync is scoped to the trusted tenant
        // (Tenant A), so it cannot locate or modify Tenant B's run. (FinishAsync has no caller
        // tenant to mismatch; the tenant-scoped lookup is the denial.)
        await using var ctxA = db.Ingestion();
        var runsA = new SyncRunRepository(ctxA, Scope(tenantA));
        await runsA.FinishAsync(runB.Id, "Completed", 0, null);

        // Tenant B's run is still Running (Tenant A modified nothing). Against the original
        // vulnerable code (which finished any run by id) this would be "Completed".
        var state = await ctxB.SyncRuns.FirstOrDefaultAsync(
            r => r.Id == runB.Id && r.TenantId == tenantB);
        Assert.Equal("Running", state?.State);
    }

    // --- 2. Tenant A cannot read Tenant B's checkpoint --------------------------------------

    [Fact]
    public async Task Tenant_A_cannot_read_Tenant_B_checkpoint()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenantA = Guid.NewGuid().ToString("N");
        var tenantB = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");

        // Tenant B stores a cursor.
        await using var ctxB = db.Ingestion();
        var cpB = new SyncCheckpointRepository(ctxB, Scope(tenantB));
        await cpB.SaveAsync(Tenant(tenantB), Source(source), "Customers", "cursor-b", DateTime.UtcNow);

        // Tenant A cannot read Tenant B's cursor (mismatched caller tenant -> denied).
        await using var ctxA = db.Ingestion();
        var cpA = new SyncCheckpointRepository(ctxA, Scope(tenantA));
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => cpA.GetAsync(Tenant(tenantB), Source(source), "Customers"));

        // Defense-in-depth: even with a matching caller tenant, Tenant A sees no cursor.
        var none = await cpA.GetAsync(Tenant(tenantA), Source(source), "Customers");
        Assert.Null(none);
    }

    // --- 3. Tenant A cannot overwrite Tenant B's checkpoint ---------------------------------

    [Fact]
    public async Task Tenant_A_cannot_overwrite_Tenant_B_checkpoint()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenantA = Guid.NewGuid().ToString("N");
        var tenantB = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");

        await using var ctxB = db.Ingestion();
        var cpB = new SyncCheckpointRepository(ctxB, Scope(tenantB));
        await cpB.SaveAsync(Tenant(tenantB), Source(source), "Customers", "cursor-b", DateTime.UtcNow);

        // Tenant A cannot overwrite Tenant B's cursor.
        await using var ctxA = db.Ingestion();
        var cpA = new SyncCheckpointRepository(ctxA, Scope(tenantA));
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => cpA.SaveAsync(Tenant(tenantB), Source(source), "Customers", "evil", DateTime.UtcNow));

        // Tenant B's cursor is unchanged.
        var stored = await cpB.GetAsync(Tenant(tenantB), Source(source), "Customers");
        Assert.Equal("cursor-b", stored?.CursorToken);
    }

    // --- 4. Tenant A cannot read or insert source records as Tenant B -----------------------

    [Fact]
    public async Task Tenant_A_cannot_read_or_insert_source_records_as_Tenant_B()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenantA = Guid.NewGuid().ToString("N");
        var tenantB = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        var id = Record(Source(source), "Customer", Guid.NewGuid().ToString("N"));

        await using var ctxA = db.Ingestion();
        var recordsA = new SourceRecordRepository(
            ctxA, NullLogger<SourceRecordRepository>.Instance, Scope(tenantA));

        // Tenant A cannot read Tenant B's source record.
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => recordsA.ExistsAsync(Tenant(tenantB), Source(source), "Customer", id.ExternalId));

        // Tenant A cannot insert a source record claiming Tenant B's identity.
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => recordsA.InsertOrUpdateAsync(
                Tenant(tenantB), id, null, Provenance(id, DateTime.UtcNow)));

        // No source record exists for Tenant B.
        var count = await CountRecordsAsync(db, Tenant(tenantB), Source(source), "Customer", id.ExternalId);
        Assert.Equal(0, count);
    }

    // --- 5. Missing trusted scope denies every tenant-owned operation -----------------------

    [Fact]
    public async Task Missing_trusted_scope_denies_every_operation()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        var id = Record(Source(source), "Customer", Guid.NewGuid().ToString("N"));
        var none = DatabaseTenantScope.None;

        await using var ctx = db.Ingestion();

        // Run state (start + finish).
        var runs = new SyncRunRepository(ctx, none);
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => runs.StartAsync(Tenant(tenant), Source(source), "Customers", "Full"));
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => runs.FinishAsync(1L, "Completed", 0, null));

        // Checkpoint (save + get).
        var cp = new SyncCheckpointRepository(ctx, none);
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => cp.SaveAsync(Tenant(tenant), Source(source), "Customers", "cursor", DateTime.UtcNow));
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => cp.GetAsync(Tenant(tenant), Source(source), "Customers"));

        // Source records (insert + exists).
        var records = new SourceRecordRepository(ctx, NullLogger<SourceRecordRepository>.Instance, none);
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => records.InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id, DateTime.UtcNow)));
        await Assert.ThrowsAnyAsync<UnauthorizedTenantException>(
            () => records.ExistsAsync(Tenant(tenant), Source(source), "Customer", id.ExternalId));
    }

    // --- 6. Correct same-tenant operations still succeed ------------------------------------

    [Fact]
    public async Task Correct_same_tenant_operations_succeed()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        var id = Record(Source(source), "Customer", Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();

        // Run: start + finish for the trusted tenant.
        var runs = new SyncRunRepository(ctx, Scope(tenant));
        var run = await runs.StartAsync(Tenant(tenant), Source(source), "Customers", "Full");
        await runs.FinishAsync(run.Id, "Completed", 0, null);
        var finished = await CountRunsAsync(db, tenant, run.Id);
        Assert.Equal(1, finished);

        // Checkpoint: save + read for the trusted tenant.
        var cp = new SyncCheckpointRepository(ctx, Scope(tenant));
        await cp.SaveAsync(Tenant(tenant), Source(source), "Customers", "cursor-a", DateTime.UtcNow);
        var stored = await cp.GetAsync(Tenant(tenant), Source(source), "Customers");
        Assert.Equal("cursor-a", stored?.CursorToken);

        // Source record: exists (false) then insert (true) for the trusted tenant.
        var records = new SourceRecordRepository(ctx, NullLogger<SourceRecordRepository>.Instance, Scope(tenant));
        Assert.False(await records.ExistsAsync(Tenant(tenant), Source(source), "Customer", id.ExternalId));
        Assert.True(await records.InsertOrUpdateAsync(
            Tenant(tenant), id, null, Provenance(id, DateTime.UtcNow)));
        var count = await CountRecordsAsync(db, Tenant(tenant), Source(source), "Customer", id.ExternalId);
        Assert.Equal(1, count);
    }
}

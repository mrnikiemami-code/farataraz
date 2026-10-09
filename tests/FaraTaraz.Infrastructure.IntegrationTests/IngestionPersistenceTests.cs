namespace FaraTaraz.Infrastructure.IntegrationTests;

using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Real PostgreSQL integration tests for the Ingestion persistence layer: durable idempotency,
/// concurrent duplicate insertion, transactional rollback, and checkpoint uniqueness
/// (ADR-010 decisions 7–9, Constitution E.20). These exercise the real database, never the
/// InMemory provider. Each test uses unique tenant/source ids so the shared database never
/// collides.
/// </summary>
public sealed class IngestionPersistenceTests
{
    private static TenantId Tenant(string value) => new(value);
    private static AccountingSourceId Source(string value) => new(value);

    private static SourceRecordId Record(AccountingSourceId sourceId, string kind, string external)
        => new(sourceId, kind, external);

    private static SourceProvenance Provenance(SourceRecordId id, DateTime retrieved)
        => new(id, new ProviderId("Mock"), id.SourceId, retrieved);

    private static async Task<int> CountRecordsAsync(IntegrationTestDb db, TenantId tenant, AccountingSourceId source, string kind, string external)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SourceRecords.CountAsync(r =>
            r.TenantId == tenant.Value &&
            r.SourceId == source.Value &&
            r.RecordKind == kind &&
            r.ExternalId == external);
    }

    [Fact]
    public async Task Idempotent_upsert_creates_exactly_one_row()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        var id = Record(Source(source), "Customer", Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();
        var repo = new SourceRecordRepository(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<SourceRecordRepository>.Instance);

        var first = await repo.InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id, DateTime.UtcNow));
        var second = await repo.InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id, DateTime.UtcNow));

        Assert.True(first);   // first delivery creates the row
        Assert.False(second); // second identical delivery is a duplicate (no new row)

        var count = await CountRecordsAsync(db, Tenant(tenant), Source(source), "Customer", id.ExternalId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Concurrent_duplicate_insert_creates_exactly_one_row()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        var id = Record(Source(source), "Customer", Guid.NewGuid().ToString("N"));

        // Two racing deliveries of the SAME source record. The unique idempotency constraint makes
        // exactly one win; the other is treated as a duplicate (ADR-010 decision 7).
        var results = await Task.WhenAll(
            DeliverAsync(db, tenant, id),
            DeliverAsync(db, tenant, id),
            DeliverAsync(db, tenant, id));

        var created = results.Count(x => x);
        Assert.Equal(1, created);

        var count = await CountRecordsAsync(db, Tenant(tenant), Source(source), "Customer", id.ExternalId);
        Assert.Equal(1, count);
    }

    private static async Task<bool> DeliverAsync(IntegrationTestDb db, string tenant, SourceRecordId id)
    {
        await using var ctx = db.Ingestion();
        var repo = new SourceRecordRepository(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<SourceRecordRepository>.Instance);
        return await repo.InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id, DateTime.UtcNow));
    }

    [Fact]
    public async Task Transaction_rollback_on_savechange_failure_persists_nothing()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");

        // Two NEW records with the SAME unique idempotency key in a single unit of work. The first
        // insert succeeds and the second violates the (TenantId, SourceId, RecordKind, ExternalId)
        // unique constraint, aborting the transaction — so NOTHING is persisted (ADR-010 decision 8).
        await using var ctx = db.Ingestion();
        var id = Record(Source(source), "Customer", Guid.NewGuid().ToString("N"));

        ctx.SourceRecords.Add(new SourceRecordEntity
        {
            TenantId = tenant, SourceId = source, RecordKind = "Customer", ExternalId = id.ExternalId,
            RetrievedAtUtc = DateTime.UtcNow,
        });
        ctx.SourceRecords.Add(new SourceRecordEntity
        {
            TenantId = tenant, SourceId = source, RecordKind = "Customer", ExternalId = id.ExternalId,
            RetrievedAtUtc = DateTime.UtcNow,
        });

        await Assert.ThrowsAnyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => ctx.SaveChangesAsync());

        var count = await CountRecordsAsync(db, Tenant(tenant), Source(source), "Customer", id.ExternalId);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Checkpoint_unique_per_scope()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");
        var checkpoint = new SyncCheckpointRepository(db.Ingestion());

        // Same scope -> upserts to one row.
        await checkpoint.SaveAsync(Tenant(tenant), Source(source), "Customers", "cursor-a", DateTime.UtcNow);
        await checkpoint.SaveAsync(Tenant(tenant), Source(source), "Customers", "cursor-b", DateTime.UtcNow);

        // Different capability -> a different scope, so a different row.
        await checkpoint.SaveAsync(Tenant(tenant), Source(source), "Products", "cursor-c", DateTime.UtcNow);

        await using var ctx = db.Ingestion();
        var total = await ctx.SyncCheckpoints.CountAsync(
            c => c.TenantId == tenant && c.SourceId == source);
        Assert.Equal(2, total);

        var stored = await ctx.SyncCheckpoints
            .FirstOrDefaultAsync(
                c => c.TenantId == tenant && c.SourceId == source && c.Capability == "Customers");
        Assert.Equal("cursor-b", stored?.CursorToken);
    }
}

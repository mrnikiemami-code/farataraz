namespace FaraTaraz.Infrastructure.IntegrationTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Contracts.SourceModel;
using FaraTaraz.Modules.Ingestion.Contracts.Synchronization;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real PostgreSQL tests for the atomic synchronization unit of work (FT-DATA-002, ADR-010
/// decision 8). Records, run state and checkpoint movement must commit together or not at all, so
/// a checkpoint can never advance beyond committed source records. Exercises the real database
/// through the public repository API — never EF Core InMemory.
/// </summary>
public sealed class IngestionUnitOfWorkTests
{
    private static TenantId Tenant(string value) => new(value);
    private static AccountingSourceId Source(string value) => new(value);

    private static DatabaseTenantScope Scope(string tenant)
        => DatabaseTenantScope.FromTrusted(
            TenantContext.FromAuthenticatedPrincipal(Tenant(tenant)));

    private static SourceRecordId Record(AccountingSourceId sourceId, string external)
        => new(sourceId, "Customer", external);

    private static SourceProvenance Provenance(SourceRecordId id)
        => new(id, new ProviderId("Mock"), id.SourceId, DateTime.UtcNow);

    private static SourceRecordRepository Records(IngestionDbContext ctx, string tenant)
        => new(ctx, NullLogger<SourceRecordRepository>.Instance, Scope(tenant));

    private static async Task<int> CountRecordsAsync(IntegrationTestDb db, string tenant, AccountingSourceId source, string external)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SourceRecords.CountAsync(r =>
            r.TenantId == tenant && r.SourceId == source.Value && r.ExternalId == external);
    }

    private static async Task<string?> ReadCursorAsync(IntegrationTestDb db, string tenant, AccountingSourceId source, string capability)
    {
        await using var ctx = db.Ingestion();
        var entity = await ctx.SyncCheckpoints.FirstOrDefaultAsync(c =>
            c.TenantId == tenant && c.SourceId == source.Value && c.Capability == capability);
        return entity?.CursorToken;
    }

    [Fact]
    public async Task Commit_persists_records_and_checkpoint_together()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();
        var uow = new IngestionUnitOfWork(ctx);
        var records = Records(ctx, tenant);
        var checkpoints = new SyncCheckpointRepository(ctx, Scope(tenant));

        await uow.ExecuteAsync(async ct =>
        {
            await records.InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id), ct);
            await checkpoints.SaveAsync(Tenant(tenant), source, "Customers", "cursor-1", DateTime.UtcNow, null, ct);
        });

        Assert.Equal(1, await CountRecordsAsync(db, tenant, source, id.ExternalId));
        Assert.Equal("cursor-1", await ReadCursorAsync(db, tenant, source, "Customers"));
    }

    [Fact]
    public async Task Failure_rolls_back_records_and_checkpoint_together()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();
        var uow = new IngestionUnitOfWork(ctx);
        var records = Records(ctx, tenant);
        var checkpoints = new SyncCheckpointRepository(ctx, Scope(tenant));

        // Records written AND the checkpoint advanced, then the step fails before commit.
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteAsync(async ct =>
        {
            await records.InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id), ct);
            await checkpoints.SaveAsync(Tenant(tenant), source, "Customers", "must-not-persist", DateTime.UtcNow, null, ct);
            throw new InvalidOperationException("sync step failed after both writes");
        }));

        // Atomic: neither the record nor the advanced cursor survived.
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source, id.ExternalId));
        Assert.Null(await ReadCursorAsync(db, tenant, source, "Customers"));
    }

    [Fact]
    public async Task Stale_checkpoint_cas_rejection_rolls_back_the_record_write()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, Guid.NewGuid().ToString("N"));

        // A committed baseline checkpoint (version 0) exists before the step begins.
        await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
            .SaveAsync(Tenant(tenant), source, "Customers", "baseline", DateTime.UtcNow, observedVersion: null);

        await using var ctx = db.Ingestion();
        var uow = new IngestionUnitOfWork(ctx);
        var records = Records(ctx, tenant);
        var checkpoints = new SyncCheckpointRepository(ctx, Scope(tenant));

        // The step writes a record, then advances the cursor with a STALE observation (null while a
        // row exists) -> the CAS guard rejects it. The record must not survive the rejected step.
        await Assert.ThrowsAsync<StaleCheckpointException>(() => uow.ExecuteAsync(async ct =>
        {
            await records.InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id), ct);
            await checkpoints.SaveAsync(Tenant(tenant), source, "Customers", "stale", DateTime.UtcNow, observedVersion: null, ct);
        }));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source, id.ExternalId));
        Assert.Equal("baseline", await ReadCursorAsync(db, tenant, source, "Customers"));
    }

    [Fact]
    public async Task Cancellation_before_commit_rolls_back()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();
        var uow = new IngestionUnitOfWork(ctx);
        var records = Records(ctx, tenant);

        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uow.ExecuteAsync(async ct =>
        {
            await records.InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id), ct);
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
        }, cts.Token));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source, id.ExternalId));
    }

    [Fact]
    public async Task Tenant_isolation_is_preserved_inside_the_unit_of_work()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenantA = Guid.NewGuid().ToString("N");
        var tenantB = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();
        var uow = new IngestionUnitOfWork(ctx);
        // Scope authorizes tenant A; the step tries to write as tenant B -> rejected fail-closed.
        var records = Records(ctx, tenantA);

        await Assert.ThrowsAsync<UnauthorizedTenantException>(() => uow.ExecuteAsync(async ct =>
        {
            await records.InsertOrUpdateAsync(Tenant(tenantB), id, null, Provenance(id), ct);
        }));

        Assert.Equal(0, await CountRecordsAsync(db, tenantB, source, id.ExternalId));
    }

    /// <summary>
    /// Reproduction of the FT-DATA-002 hazard: WITHOUT a unit of work each write autocommits
    /// independently, so a later failure leaves the source record committed AND the checkpoint
    /// advanced (premature advancement) — exactly the partial persistence this unit of work
    /// prevents.
    /// </summary>
    [Fact]
    public async Task Without_unit_of_work_partial_persistence_and_premature_advancement_are_possible()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();
        var records = Records(ctx, tenant);
        var checkpoints = new SyncCheckpointRepository(ctx, Scope(tenant));

        // No transaction: both writes autocommit immediately.
        await records.InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id));
        await checkpoints.SaveAsync(Tenant(tenant), source, "Customers", "advanced", DateTime.UtcNow, observedVersion: null);

        // The synchronization step then fails (here: a tenant-authority violation on the next write).
        var other = Record(source, Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            records.InsertOrUpdateAsync(Tenant("other-tenant"), other, null, Provenance(other)));

        // Despite the failed step, the record and the ADVANCED cursor both persisted — the hazard.
        Assert.Equal(1, await CountRecordsAsync(db, tenant, source, id.ExternalId));
        Assert.Equal("advanced", await ReadCursorAsync(db, tenant, source, "Customers"));
    }
}

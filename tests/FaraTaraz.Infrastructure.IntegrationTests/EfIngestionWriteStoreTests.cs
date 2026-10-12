namespace FaraTaraz.Infrastructure.IntegrationTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Application.Ports;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real PostgreSQL tests for the M2 persistence adapter <see cref="EfIngestionWriteStore"/>:
/// one atomic transaction across run state + records + checkpoint, tenant binding from the trusted
/// scope only, CAS, rollback, cancellation, idempotency, nullable-fingerprint identity and session
/// lifetime.
/// </summary>
public sealed class EfIngestionWriteStoreTests
{
    private static TenantId Tenant(string value) => new(value);
    private static AccountingSourceId Source(string value) => new(value);

    private static DatabaseTenantScope Scope(string tenant)
        => DatabaseTenantScope.FromTrusted(TenantContext.FromAuthenticatedPrincipal(Tenant(tenant)));

    private static SourceRecordId Record(AccountingSourceId source, string external)
        => new(source, "Customer", external);

    private static SourceRecordVersion Version(string fingerprint) => new() { ContentFingerprint = fingerprint };

    private static SourceProvenance Provenance(SourceRecordId id, AccountingSourceId source)
        => new(id, new ProviderId("Mock"), source, DateTime.UtcNow);

    private static EfIngestionWriteStore Store(IntegrationTestDb db, IngestionDbContext ctx, string tenant, DatabaseTenantScope scope)
        => new(
            new IngestionUnitOfWork(ctx),
            new SourceRecordRepository(ctx, NullLogger<SourceRecordRepository>.Instance, scope),
            new SyncCheckpointRepository(ctx, scope),
            new SyncRunRepository(ctx, scope),
            scope);

    private static async Task<int> CountRecordsAsync(IntegrationTestDb db, string tenant, AccountingSourceId source)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SourceRecords.CountAsync(r => r.TenantId == tenant && r.SourceId == source.Value);
    }

    private static async Task<string?> ReadCursorAsync(IntegrationTestDb db, string tenant, AccountingSourceId source)
    {
        await using var ctx = db.Ingestion();
        return (await ctx.SyncCheckpoints.FirstOrDefaultAsync(c => c.TenantId == tenant && c.SourceId == source.Value))?.CursorToken;
    }

    private static async Task<string?> ReadRunStateAsync(IntegrationTestDb db, long runId)
    {
        await using var ctx = db.Ingestion();
        return (await ctx.SyncRuns.FirstOrDefaultAsync(r => r.Id == runId))?.State;
    }

    [Fact]
    public async Task Commit_persists_run_records_and_checkpoint_atomically()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, "C1");

        await using var ctx = db.Ingestion();
        var store = Store(db, ctx, tenant, Scope(tenant));

        var runId = await store.ExecuteInTransactionAsync<long>(async (IIngestionWriteSession session, CancellationToken ct) =>
        {
            var run = await session.StartRunAsync(source, "Customers", SyncMode.Full, ct);
            await session.InsertRecordAsync(id, Version("fp1"), Provenance(id, source), ct);
            await session.SaveCheckpointAsync(source, "Customers", "cursor-1", DateTime.UtcNow, null, ct);
            await session.FinishRunAsync(run, IngestionRunOutcome.Completed, 1, null, ct);
            return run;
        });

        Assert.Equal(1, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("cursor-1", await ReadCursorAsync(db, tenant, source));
        Assert.Equal("Completed", await ReadRunStateAsync(db, runId));
    }

    [Fact]
    public async Task Failure_rolls_back_run_records_and_checkpoint()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, "C1");

        await using var ctx = db.Ingestion();
        var store = Store(db, ctx, tenant, Scope(tenant));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ExecuteInTransactionAsync<int>(async (IIngestionWriteSession session, CancellationToken ct) =>
        {
            await session.StartRunAsync(source, "Customers", SyncMode.Full, ct);
            await session.InsertRecordAsync(id, Version("fp1"), Provenance(id, source), ct);
            await session.SaveCheckpointAsync(source, "Customers", "cursor-1", DateTime.UtcNow, null, ct);
            throw new InvalidOperationException("boom after all writes");
        }));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Null(await ReadCursorAsync(db, tenant, source));
        await using var read = db.Ingestion();
        Assert.Equal(0, await read.SyncRuns.CountAsync(r => r.TenantId == tenant));
    }

    [Fact]
    public async Task Stale_checkpoint_cas_rejects_and_rolls_back_the_record_write()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, "C1");

        await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
            .SaveAsync(Tenant(tenant), source, "Customers", "baseline", DateTime.UtcNow, observedVersion: null);

        await using var ctx = db.Ingestion();
        var store = Store(db, ctx, tenant, Scope(tenant));

        await Assert.ThrowsAsync<StaleCheckpointException>(() => store.ExecuteInTransactionAsync<int>(async (IIngestionWriteSession session, CancellationToken ct) =>
        {
            await session.InsertRecordAsync(id, Version("fp1"), Provenance(id, source), ct);
            // Stale observation: a row already exists (version 0), but we pass null.
            await session.SaveCheckpointAsync(source, "Customers", "stale", DateTime.UtcNow, null, ct);
            return 0;
        }));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("baseline", await ReadCursorAsync(db, tenant, source));
    }

    [Fact]
    public async Task Cancellation_before_commit_leaves_no_partial_state()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();
        var store = Store(db, ctx, tenant, Scope(tenant));

        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ExecuteInTransactionAsync<int>(async (IIngestionWriteSession session, CancellationToken ct) =>
        {
            await session.StartRunAsync(source, "Customers", SyncMode.Full, ct);
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return 0;
        }, cts.Token));

        await using var read = db.Ingestion();
        Assert.Equal(0, await read.SyncRuns.CountAsync(r => r.TenantId == tenant));
    }

    [Fact]
    public async Task Untrusted_or_missing_scope_is_rejected_fail_closed()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        await using var ctx = db.Ingestion();
        var store = Store(db, ctx, "tenant-a", DatabaseTenantScope.None);

        await Assert.ThrowsAsync<UnauthorizedTenantException>(
            () => store.ExecuteInTransactionAsync((_, _) => Task.FromResult(0)));
        await Assert.ThrowsAsync<UnauthorizedTenantException>(
            () => store.ReadAsync(Source("s"), "Customers"));
    }

    [Fact]
    public async Task Null_fingerprint_row_is_distinguished_from_a_missing_identity()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, "C1");
        var missing = Record(source, "MISSING");

        // Seed an existing row with a NULL fingerprint (no version).
        await using (var seed = db.Ingestion())
        {
            await new SourceRecordRepository(seed, NullLogger<SourceRecordRepository>.Instance, Scope(tenant))
                .InsertOrUpdateAsync(Tenant(tenant), id, null, Provenance(id, source));
        }

        await using var ctx = db.Ingestion();
        var store = Store(db, ctx, tenant, Scope(tenant));

        await store.ExecuteInTransactionAsync<int>(async (IIngestionWriteSession session, CancellationToken ct) =>
        {
            var existing = await session.ProbeRecordAsync(id, ct);
            var absent = await session.ProbeRecordAsync(missing, ct);

            Assert.NotNull(existing);                     // exists...
            Assert.Null(existing!.ContentFingerprint);    // ...with a null value, not absent
            Assert.Null(absent);                          // genuinely absent
            return 0;
        });
    }

    [Fact]
    public async Task Duplicate_insert_remains_idempotent()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = Record(source, "C1");

        await using var ctx = db.Ingestion();
        var store = Store(db, ctx, tenant, Scope(tenant));

        var first = await store.ExecuteInTransactionAsync((s, ct) => s.InsertRecordAsync(id, Version("fp1"), Provenance(id, source), ct));
        var second = await store.ExecuteInTransactionAsync((s, ct) => s.InsertRecordAsync(id, Version("fp1"), Provenance(id, source), ct));

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, await CountRecordsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Session_cannot_be_used_after_the_transaction_completes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();
        var store = Store(db, ctx, tenant, Scope(tenant));

        IIngestionWriteSession? captured = null;
        await store.ExecuteInTransactionAsync((session, _) =>
        {
            captured = session;
            return Task.FromResult(0);
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => captured!.StartRunAsync(source, "Customers", SyncMode.Full));
    }

    [Theory]
    [InlineData(IngestionRunOutcome.Failed, "Failed")]
    [InlineData(IngestionRunOutcome.Cancelled, "Cancelled")]
    public async Task Typed_run_outcomes_are_mapped_to_storage_states(IngestionRunOutcome outcome, string expected)
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        await using var ctx = db.Ingestion();
        var store = Store(db, ctx, tenant, Scope(tenant));

        var runId = await store.ExecuteInTransactionAsync<long>(async (IIngestionWriteSession session, CancellationToken ct) =>
        {
            var run = await session.StartRunAsync(source, "Customers", SyncMode.Full, ct);
            await session.FinishRunAsync(run, outcome, 0, "detail", ct);
            return run;
        });

        Assert.Equal(expected, await ReadRunStateAsync(db, runId));
    }
}

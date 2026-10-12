namespace FaraTaraz.Infrastructure.IntegrationTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Authorization;
using FaraTaraz.Modules.AccountingSources.Capabilities;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.Ingestion.Infrastructure;
using FaraTaraz.Modules.MasterData;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real PostgreSQL integration tests for the first W3 ingestion-engine orchestration slice
/// (<see cref="IngestionRunCoordinator"/>): ownership admission precedes provider work, and
/// records + checkpoint + run state commit atomically. Finally closes W3-R1.
/// </summary>
public sealed class IngestionRunCoordinatorTests
{
    private static TenantId Tenant(string value) => new(value);
    private static AccountingSourceId Source(string value) => new(value);

    private static DatabaseTenantScope Scope(string tenant)
        => DatabaseTenantScope.FromTrusted(TenantContext.FromAuthenticatedPrincipal(Tenant(tenant)));

    private static TenantContext Trusted(string tenant)
        => TenantContext.FromAuthenticatedPrincipal(Tenant(tenant));

    private static SourceCustomer Customer(AccountingSourceId source, string code, string fingerprint)
        => new(
            new SourceRecordId(source, "Customer", code),
            new ExternalCustomerId(source, code),
            code,
            "Customer " + code,
            new SourceRecordVersion { ContentFingerprint = fingerprint });

    private static SyncBatch<SourceCustomer> Complete(AccountingSourceId source, params SourceCustomer[] records)
        => SyncBatch<SourceCustomer>.Complete(records);

    private static SyncBatch<SourceCustomer> Continue(AccountingSourceId source, string token, params SourceCustomer[] records)
        => SyncBatch<SourceCustomer>.Continue(records, new SyncCursor(token, new SyncCursorScope(source, "Customers")));

    private static IngestionRunCoordinator Coordinator(
        IngestionDbContext ctx, string tenant, IAccountingSourceOwnership ownership)
        => new(
            new IngestionUnitOfWork(ctx),
            new SourceRecordRepository(ctx, NullLogger<SourceRecordRepository>.Instance, Scope(tenant)),
            new SyncCheckpointRepository(ctx, Scope(tenant)),
            new SyncRunRepository(ctx, Scope(tenant)),
            ownership);

    private static async Task<int> CountRecordsAsync(IntegrationTestDb db, string tenant, AccountingSourceId source)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SourceRecords.CountAsync(r => r.TenantId == tenant && r.SourceId == source.Value);
    }

    private static async Task<int> CountRunsAsync(IntegrationTestDb db, string tenant, AccountingSourceId source)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SyncRuns.CountAsync(r => r.TenantId == tenant && r.SourceId == source.Value);
    }

    private static async Task<string?> ReadCursorAsync(IntegrationTestDb db, string tenant, AccountingSourceId source)
    {
        await using var ctx = db.Ingestion();
        var row = await ctx.SyncCheckpoints.FirstOrDefaultAsync(c => c.TenantId == tenant && c.SourceId == source.Value);
        return row?.CursorToken;
    }

    private static async Task<string?> ReadFingerprintAsync(IntegrationTestDb db, string tenant, SourceRecordId id)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SourceRecords
            .Where(r => r.TenantId == tenant && r.SourceId == id.SourceId.Value && r.ExternalId == id.ExternalId)
            .Select(r => r.ContentFingerprint)
            .FirstOrDefaultAsync();
    }

    // 1. Successful one-page ingestion: records + checkpoint + run state committed together.
    [Fact]
    public async Task Successful_one_page_ingestion_commits_records_checkpoint_and_run_together()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(
            Continue(source, "next-cursor", Customer(source, "C1", "fp1"), Customer(source, "C2", "fp2"))));

        await using var ctx = db.Ingestion();
        var coordinator = Coordinator(ctx, tenant, FakeOwnership.OwnsAll());

        var result = await coordinator.ExecuteAsync(
            Trusted(tenant), new SyncRequest(source, SyncMode.Full, batchSize: 10), capability, new ProviderId("Mock"));

        Assert.Equal(2, result.RecordsCreated);
        Assert.False(result.IsComplete);
        Assert.Equal("next-cursor", result.NextCursorToken);
        Assert.Equal(2, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("next-cursor", await ReadCursorAsync(db, tenant, source));
        Assert.Equal(1, await CountRunsAsync(db, tenant, source));
    }

    // 2. Untrusted tenant: zero provider calls and zero writes.
    [Fact]
    public async Task Untrusted_tenant_is_rejected_with_zero_provider_calls_and_zero_writes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));

        await using var ctx = db.Ingestion();
        var coordinator = Coordinator(ctx, tenant, FakeOwnership.OwnsAll());

        var untrusted = new TenantContext(Tenant(tenant), TenantContextOrigin.ClientInput);

        await Assert.ThrowsAsync<UnauthorizedTenantException>(() => coordinator.ExecuteAsync(
            untrusted, new SyncRequest(source, SyncMode.Full), capability, new ProviderId("Mock")));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    // 3. Unknown / cross-tenant source: zero provider calls and zero writes.
    [Fact]
    public async Task Source_not_owned_by_tenant_is_rejected_with_zero_provider_calls_and_zero_writes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));

        await using var ctx = db.Ingestion();
        var coordinator = Coordinator(ctx, tenant, FakeOwnership.OwnsNone());

        await Assert.ThrowsAsync<UnauthorizedSourceException>(() => coordinator.ExecuteAsync(
            Trusted(tenant), new SyncRequest(source, SyncMode.Full), capability, new ProviderId("Mock")));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    // 4. Ownership lookup failure fails closed (no provider, no writes).
    [Fact]
    public async Task Ownership_lookup_failure_fails_closed()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));

        await using var ctx = db.Ingestion();
        var coordinator = Coordinator(ctx, tenant, FakeOwnership.Fails());

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            Trusted(tenant), new SyncRequest(source, SyncMode.Full), capability, new ProviderId("Mock")));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    // 5. Persistence failure: full rollback (no record, no checkpoint, no run).
    [Fact]
    public async Task Persistence_failure_rolls_back_everything()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        // An external id far beyond the column length forces a real database failure mid-step.
        var oversized = new string('x', 2000);
        var capability = new FakeCustomers((_, _) => Task.FromResult(
            Continue(source, "cursor", Customer(source, oversized, "fp1"))));

        await using var ctx = db.Ingestion();
        var coordinator = Coordinator(ctx, tenant, FakeOwnership.OwnsAll());

        await Assert.ThrowsAnyAsync<Exception>(() => coordinator.ExecuteAsync(
            Trusted(tenant), new SyncRequest(source, SyncMode.Full), capability, new ProviderId("Mock")));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Null(await ReadCursorAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    // 6. Stale checkpoint CAS: no partial writes and no cursor advancement.
    [Fact]
    public async Task Stale_checkpoint_cas_rolls_back_records_and_leaves_cursor_unchanged()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        // The capability advances the cursor as a side effect (a concurrent writer) AFTER the
        // coordinator observed the absent checkpoint but BEFORE it saves -> the CAS must reject.
        var capability = new FakeCustomers(async (_, _) =>
        {
            await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
                .SaveAsync(Tenant(tenant), source, "Customers", "concurrent", DateTime.UtcNow, observedVersion: null);
            return Continue(source, "engine-cursor", Customer(source, "C1", "fp1"));
        });

        await using var ctx = db.Ingestion();
        var coordinator = Coordinator(ctx, tenant, FakeOwnership.OwnsAll());

        await Assert.ThrowsAsync<StaleCheckpointException>(() => coordinator.ExecuteAsync(
            Trusted(tenant), new SyncRequest(source, SyncMode.Full), capability, new ProviderId("Mock")));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("concurrent", await ReadCursorAsync(db, tenant, source)); // never advanced by the engine
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    // 7. Cancellation before commit: no partial state.
    [Fact]
    public async Task Cancellation_before_commit_leaves_no_partial_state()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        using var cts = new CancellationTokenSource();
        var capability = new FakeCustomers((_, _) =>
        {
            cts.Cancel(); // the step is cancelled before the atomic write phase begins
            return Task.FromResult(Continue(source, "cursor", Customer(source, "C1", "fp1")));
        });

        await using var ctx = db.Ingestion();
        var coordinator = Coordinator(ctx, tenant, FakeOwnership.OwnsAll());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.ExecuteAsync(
            Trusted(tenant), new SyncRequest(source, SyncMode.Full), capability, new ProviderId("Mock"), cts.Token));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Null(await ReadCursorAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    // 8. Duplicate delivery: no duplicate records.
    [Fact]
    public async Task Duplicate_delivery_creates_no_duplicate_records()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(
            Complete(source, Customer(source, "C1", "fp1"), Customer(source, "C2", "fp2"))));

        await using var ctx = db.Ingestion();
        var coordinator = Coordinator(ctx, tenant, FakeOwnership.OwnsAll());
        var request = new SyncRequest(source, SyncMode.Full);

        var first = await coordinator.ExecuteAsync(Trusted(tenant), request, capability, new ProviderId("Mock"));
        var second = await coordinator.ExecuteAsync(Trusted(tenant), request, capability, new ProviderId("Mock"));

        Assert.Equal(2, first.RecordsCreated);
        Assert.Equal(0, second.RecordsCreated); // identical duplicate -> idempotent no-op
        Assert.Equal(2, await CountRecordsAsync(db, tenant, source));
    }

    // 9. Unsupported changed-record semantics: fail closed rather than silently discard the update.
    [Fact]
    public async Task Changed_record_is_rejected_fail_closed_and_does_not_alter_stored_state()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = new SourceRecordId(source, "Customer", "C1");

        await using var ctx = db.Ingestion();
        var coordinator = Coordinator(ctx, tenant, FakeOwnership.OwnsAll());
        var request = new SyncRequest(source, SyncMode.Full);

        var first = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));
        await coordinator.ExecuteAsync(Trusted(tenant), request, first, new ProviderId("Mock"));

        // Same identity, different content -> unsupported changed record -> fail closed, rollback.
        var changed = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp2"))));
        await Assert.ThrowsAsync<UnsupportedChangedRecordException>(() => coordinator.ExecuteAsync(
            Trusted(tenant), request, changed, new ProviderId("Mock")));

        Assert.Equal(1, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("fp1", await ReadFingerprintAsync(db, tenant, id)); // stored value never altered
        Assert.Equal(1, await CountRunsAsync(db, tenant, source)); // the rejected run rolled back
    }

    private sealed class FakeCustomers : ISyncablePort<SourceCustomer>
    {
        private readonly Func<SyncRequest, CancellationToken, Task<SyncBatch<SourceCustomer>>> _sync;

        public FakeCustomers(Func<SyncRequest, CancellationToken, Task<SyncBatch<SourceCustomer>>> sync)
            => _sync = sync;

        public string CapabilityName => "Customers";

        public SyncModeSupport SyncModeSupport => SyncModeSupport.FullAndIncremental;

        public int Calls { get; private set; }

        public Task<SyncBatch<SourceCustomer>> SyncAsync(SyncRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return _sync(request, cancellationToken);
        }
    }

    private sealed class FakeOwnership : IAccountingSourceOwnership
    {
        private readonly Func<Task<bool>> _impl;

        private FakeOwnership(Func<Task<bool>> impl) => _impl = impl;

        public static FakeOwnership OwnsAll() => new(() => Task.FromResult(true));
        public static FakeOwnership OwnsNone() => new(() => Task.FromResult(false));
        public static FakeOwnership Fails() => new(() => throw new InvalidOperationException("ownership lookup failed"));

        public Task<bool> IsOwnedByAsync(TenantId tenantId, AccountingSourceId sourceId, CancellationToken cancellationToken = default)
            => _impl();
    }
}

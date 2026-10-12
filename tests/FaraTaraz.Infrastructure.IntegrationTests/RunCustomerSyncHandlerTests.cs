namespace FaraTaraz.Infrastructure.IntegrationTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Authorization;
using FaraTaraz.Modules.AccountingSources.Capabilities;
using FaraTaraz.Modules.Ingestion.Application.Ports;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Commands;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using FaraTaraz.Modules.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real PostgreSQL tests for the Application-owned customer ingestion use case
/// (<see cref="RunCustomerSyncHandler"/>), driven through the real M2 persistence adapter
/// (<see cref="EfIngestionWriteStore"/>). Covers the migration of M3/M4: admission order, trusted
/// tenant/scope agreement, single atomic write step, CAS, resume, nullable-fingerprint identity,
/// duplicate/changed handling and rollback.
/// </summary>
public sealed partial class RunCustomerSyncHandlerTests
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

    private static SourceCustomer CustomerNoVersion(AccountingSourceId source, string code)
        => new(
            new SourceRecordId(source, "Customer", code),
            new ExternalCustomerId(source, code),
            code,
            "Customer " + code,
            null);

    private static SyncBatch<SourceCustomer> Complete(AccountingSourceId source, params SourceCustomer[] records)
        => SyncBatch<SourceCustomer>.Complete(records);

    private static SyncBatch<SourceCustomer> Continue(AccountingSourceId source, string token, params SourceCustomer[] records)
        => SyncBatch<SourceCustomer>.Continue(records, new SyncCursor(token, new SyncCursorScope(source, "Customers")));

    /// <summary>Builds the use case over the REAL M2 adapter bound to <paramref name="bindingTenant"/>.</summary>
    private static RunCustomerSyncHandler Handler(
        IngestionDbContext ctx,
        string bindingTenant,
        IAccountingSourceOwnership ownership,
        ISyncablePort<SourceCustomer> capability,
        IAccountingSourceProviderResolver? providerResolver = null)
    {
        var scope = Scope(bindingTenant);
        var store = new EfIngestionWriteStore(
            new IngestionUnitOfWork(ctx),
            new SourceRecordRepository(ctx, NullLogger<SourceRecordRepository>.Instance, scope),
            new SyncCheckpointRepository(ctx, scope),
            new SyncRunRepository(ctx, scope),
            scope);

        return new RunCustomerSyncHandler(
            store, store, store, ownership, providerResolver ?? FakeProviderResolver.Resolving("Mock"), capability);
    }

    private static Task<IngestionRunResult> RunAsync(
        RunCustomerSyncHandler handler, string tenant, SyncRequest request,
        CustomerSyncIntent intent = CustomerSyncIntent.Continue)
        => handler.Handle(new RunCustomerSyncCommand(Trusted(tenant), request, intent), default);

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

    private static async Task<string?> ReadStateAsync(IntegrationTestDb db, string tenant, AccountingSourceId source)
    {
        await using var ctx = db.Ingestion();
        var row = await ctx.SyncCheckpoints.FirstOrDefaultAsync(c => c.TenantId == tenant && c.SourceId == source.Value);
        return row?.State;
    }

    private static async Task<string?> ReadFingerprintAsync(IntegrationTestDb db, string tenant, SourceRecordId id)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SourceRecords
            .Where(r => r.TenantId == tenant && r.SourceId == id.SourceId.Value && r.ExternalId == id.ExternalId)
            .Select(r => r.ContentFingerprint)
            .FirstOrDefaultAsync();
    }

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
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        var result = await RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full, batchSize: 10));

        Assert.Equal(2, result.RecordsCreated);
        Assert.False(result.IsComplete);
        Assert.Equal("next-cursor", result.NextCursorToken);
        Assert.Equal(2, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("next-cursor", await ReadCursorAsync(db, tenant, source));
        Assert.Equal(1, await CountRunsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Untrusted_tenant_is_rejected_with_zero_provider_calls_and_zero_writes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        var untrusted = new TenantContext(Tenant(tenant), TenantContextOrigin.ClientInput);
        await Assert.ThrowsAsync<UnauthorizedTenantException>(() => handler.Handle(
            new RunCustomerSyncCommand(untrusted, new SyncRequest(source, SyncMode.Full)), default));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Trusted_context_and_database_scope_mismatch_fails_closed_before_provider_and_writes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenantA = Guid.NewGuid().ToString("N");
        var tenantB = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));

        await using var ctx = db.Ingestion();
        // The adapter is bound to tenant B, but the command's trusted context is tenant A.
        var handler = Handler(ctx, tenantB, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAsync<UnauthorizedTenantException>(
            () => RunAsync(handler, tenantA, new SyncRequest(source, SyncMode.Full)));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenantA, source));
        Assert.Equal(0, await CountRecordsAsync(db, tenantB, source));
    }

    [Fact]
    public async Task Source_not_owned_by_tenant_is_rejected_with_zero_provider_calls_and_zero_writes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsNone(), capability);

        await Assert.ThrowsAsync<UnauthorizedSourceException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full)));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Ownership_lookup_failure_fails_closed()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.Fails(), capability);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full)));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Persistence_failure_rolls_back_everything()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var oversized = new string('x', 2000);
        var capability = new FakeCustomers((_, _) => Task.FromResult(
            Continue(source, "cursor", Customer(source, oversized, "fp1"))));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAnyAsync<Exception>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full)));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Null(await ReadCursorAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Stale_checkpoint_cas_rolls_back_records_and_leaves_cursor_unchanged()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        var capability = new FakeCustomers(async (_, _) =>
        {
            await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
                .SaveAsync(Tenant(tenant), source, "Customers", "concurrent", DateTime.UtcNow, observedVersion: null);
            return Continue(source, "engine-cursor", Customer(source, "C1", "fp1"));
        });

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAsync<StaleCheckpointException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full)));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("concurrent", await ReadCursorAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

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
            cts.Cancel();
            return Task.FromResult(Continue(source, "cursor", Customer(source, "C1", "fp1")));
        });

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(
            new RunCustomerSyncCommand(Trusted(tenant), new SyncRequest(source, SyncMode.Full)),
            cts.Token));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Null(await ReadCursorAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Duplicate_delivery_creates_no_duplicate_records()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(
            Continue(source, "p1", Customer(source, "C1", "fp1"), Customer(source, "C2", "fp2"))));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);
        var request = new SyncRequest(source, SyncMode.Full);

        var first = await RunAsync(handler, tenant, request);
        var second = await RunAsync(handler, tenant, request);

        Assert.Equal(2, first.RecordsCreated);
        Assert.Equal(0, second.RecordsCreated);
        Assert.Equal(2, await CountRecordsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Changed_record_is_rejected_fail_closed_and_does_not_alter_stored_state()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = new SourceRecordId(source, "Customer", "C1");

        await using var ctx = db.Ingestion();
        var request = new SyncRequest(source, SyncMode.Full);

        var first = new FakeCustomers((_, _) => Task.FromResult(Continue(source, "p1", Customer(source, "C1", "fp1"))));
        await RunAsync(Handler(ctx, tenant, FakeOwnership.OwnsAll(), first), tenant, request);

        var changed = new FakeCustomers((_, _) => Task.FromResult(Continue(source, "p2", Customer(source, "C1", "fp2"))));
        await Assert.ThrowsAsync<UnsupportedChangedRecordException>(
            () => RunAsync(Handler(ctx, tenant, FakeOwnership.OwnsAll(), changed), tenant, request));

        Assert.Equal(1, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("fp1", await ReadFingerprintAsync(db, tenant, id));
        Assert.Equal(1, await CountRunsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Resumed_step_uses_the_persisted_continuation_cursor()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
            .SaveAsync(Tenant(tenant), source, "Customers", "resume-token", DateTime.UtcNow, observedVersion: null);

        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source)));
        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full));

        Assert.Equal("resume-token", capability.LastRequest!.Cursor?.Token);
        Assert.Equal(source, capability.LastRequest!.Cursor?.Scope.SourceId);
        Assert.Equal("Customers", capability.LastRequest!.Cursor?.Scope.Capability);
    }

    [Fact]
    public async Task No_checkpoint_requests_the_first_page_without_a_cursor()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source)));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full));

        Assert.Null(capability.LastRequest!.Cursor);
    }

    [Fact]
    public async Task Conflicting_supplied_and_persisted_cursors_are_rejected()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
            .SaveAsync(Tenant(tenant), source, "Customers", "persisted-token", DateTime.UtcNow, observedVersion: null);

        var supplied = new SyncCursor("different-token", new SyncCursorScope(source, "Customers"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source)));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAsync<InvalidSyncCursorException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full, supplied)));

        Assert.Equal(0, capability.Calls);
        Assert.Equal("persisted-token", await ReadCursorAsync(db, tenant, source));
    }

    [Fact]
    public async Task Mismatched_supplied_cursor_scope_is_rejected()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var wrongScope = new SyncCursor("token", new SyncCursorScope(source, "Products"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source)));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAsync<SyncCursorScopeMismatchException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full, wrongScope)));

        Assert.Equal(0, capability.Calls);
    }

    [Fact]
    public async Task Stored_null_fingerprint_is_not_misclassified_as_absent()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var id = new SourceRecordId(source, "Customer", "C1");

        await using (var seed = db.Ingestion())
        {
            var repo = new SourceRecordRepository(seed, NullLogger<SourceRecordRepository>.Instance, Scope(tenant));
            await repo.InsertOrUpdateAsync(
                Tenant(tenant), id, null, new SourceProvenance(id, new ProviderId("Mock"), source, DateTime.UtcNow));
        }

        await using var ctx = db.Ingestion();
        var request = new SyncRequest(source, SyncMode.Full);

        var changed = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));
        await Assert.ThrowsAsync<UnsupportedChangedRecordException>(
            () => RunAsync(Handler(ctx, tenant, FakeOwnership.OwnsAll(), changed), tenant, request));
        Assert.Equal(1, await CountRecordsAsync(db, tenant, source));
        Assert.Null(await ReadFingerprintAsync(db, tenant, id));

        var identical = new FakeCustomers((_, _) => Task.FromResult(Complete(source, CustomerNoVersion(source, "C1"))));
        var result = await RunAsync(Handler(ctx, tenant, FakeOwnership.OwnsAll(), identical), tenant, request);
        Assert.Equal(0, result.RecordsCreated);
        Assert.Equal(1, await CountRecordsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Resumed_step_with_stale_cas_does_not_advance_the_checkpoint_or_write_records()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
            .SaveAsync(Tenant(tenant), source, "Customers", "c1", DateTime.UtcNow, observedVersion: null);

        var capability = new FakeCustomers(async (_, _) =>
        {
            await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
                .SaveAsync(Tenant(tenant), source, "Customers", "c2", DateTime.UtcNow, observedVersion: 0L);
            return Continue(source, "engine-cursor", Customer(source, "C1", "fp1"));
        });

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAsync<StaleCheckpointException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full)));

        Assert.Equal("c1", capability.LastRequest!.Cursor?.Token);
        Assert.Equal("c2", await ReadCursorAsync(db, tenant, source));
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    private sealed class FakeCustomers : ISyncablePort<SourceCustomer>
    {
        private readonly Func<SyncRequest, CancellationToken, Task<SyncBatch<SourceCustomer>>> _sync;

        public FakeCustomers(Func<SyncRequest, CancellationToken, Task<SyncBatch<SourceCustomer>>> sync)
            => _sync = sync;

        public string CapabilityName => "Customers";

        public SyncModeSupport SyncModeSupport => SyncModeSupport.FullAndIncremental;

        public int Calls { get; private set; }

        public SyncRequest? LastRequest { get; private set; }

        public Task<SyncBatch<SourceCustomer>> SyncAsync(SyncRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return _sync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task Provenance_uses_the_resolved_provider_identity()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability, FakeProviderResolver.Resolving("ResolvedProvider"));
        await RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full));

        await using var read = db.Ingestion();
        var storedProvider = await read.SourceRecords
            .Where(r => r.TenantId == tenant && r.SourceId == source.Value)
            .Select(r => r.Provider)
            .FirstOrDefaultAsync();
        Assert.Equal("ResolvedProvider", storedProvider);
    }

    [Fact]
    public async Task Missing_provider_configuration_fails_closed_before_provider_and_writes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source, Customer(source, "C1", "fp1"))));

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability, FakeProviderResolver.Missing());

        await Assert.ThrowsAsync<UnauthorizedSourceException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full)));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    private sealed class FakeProviderResolver : IAccountingSourceProviderResolver
    {
        private readonly ProviderId? _provider;

        private FakeProviderResolver(ProviderId? provider) => _provider = provider;

        public static FakeProviderResolver Resolving(string provider) => new(new ProviderId(provider));
        public static FakeProviderResolver Missing() => new(null);

        public Task<ProviderId?> ResolveProviderAsync(
            TenantId tenantId, AccountingSourceId sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_provider);
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

namespace FaraTaraz.Infrastructure.IntegrationTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Authorization;
using FaraTaraz.Modules.AccountingSources.Capabilities;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Authorization;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Commands;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;
using FaraTaraz.Modules.Ingestion.Contracts.SourceModel;
using FaraTaraz.Modules.Ingestion.Contracts.Synchronization;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using FaraTaraz.Modules.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real PostgreSQL tests for the shared active-source admission gate
/// (task <c>FT-W4-B1-DISABLED-SOURCE-ADMISSION</c>).
///
/// An AccountingSource the trusted Tenant owns but which is administratively
/// <see cref="AccountingSourceStatus.Disabled"/> must be refused in BOTH Application handlers —
/// the durable Command path and the read-only Query preview — BEFORE any provider resolution,
/// provider call or ingestion write. Ownership semantics are unchanged: a missing or foreign
/// source is still denied by the ownership oracle, and activation is never consulted for it.
/// </summary>
public sealed class AccountingSourceActivationAdmissionTests
{
    private static TenantId Tenant(string value) => new(value);
    private static AccountingSourceId Source(string value) => new(value);

    private static DatabaseTenantScope Trusted(string tenant)
        => DatabaseTenantScope.FromTrusted(TenantContext.FromAuthenticatedPrincipal(Tenant(tenant)));

    private static TenantContext TrustedContext(string tenant)
        => TenantContext.FromAuthenticatedPrincipal(Tenant(tenant));

    private static async Task SeedAsync(IntegrationTestDb db, string tenant, string source, AccountingSourceStatus status)
    {
        await using var seed = db.AccountingSources();
        if (!await seed.Tenants.AnyAsync(t => t.Id == tenant))
        {
            seed.Tenants.Add(new TenantEntity { Id = tenant, Name = "T " + tenant });
            await seed.SaveChangesAsync();
        }

        var t = await seed.Tenants.FirstAsync(x => x.Id == tenant);
        seed.AccountingSources.Add(new AccountingSourceEntity
        {
            Id = source,
            TenantId = tenant,
            Provider = "Mock",
            DisplayName = "S " + source,
            Status = (int)status,
            Tenant = t,
        });
        await seed.SaveChangesAsync();
    }

    private static SourceCustomer Customer(AccountingSourceId source, string code, string fingerprint)
        => new(
            new SourceRecordId(source, "Customer", code),
            new ExternalCustomerId(source, code),
            code,
            "Customer " + code,
            new SourceRecordVersion { ContentFingerprint = fingerprint });

    private static EfIngestionWriteStore WriteStore(IngestionDbContext ctx, DatabaseTenantScope scope)
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

    private static async Task<int> CountRunsAsync(IntegrationTestDb db, string tenant, AccountingSourceId source)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SyncRuns.CountAsync(r => r.TenantId == tenant && r.SourceId == source.Value);
    }

    private static async Task<int> CountCheckpointsAsync(IntegrationTestDb db, string tenant, AccountingSourceId source)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SyncCheckpoints.CountAsync(c => c.TenantId == tenant && c.SourceId == source.Value);
    }

    // --- The activation oracle itself (real PostgreSQL, trusted scope) ---------------------

    [Fact]
    public async Task Activation_reports_disabled_and_enabled_correctly()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var enabled = Source(Guid.NewGuid().ToString("N"));
        var disabled = Source(Guid.NewGuid().ToString("N"));
        var syncing = Source(Guid.NewGuid().ToString("N"));
        await SeedAsync(db, tenant, enabled.Value, AccountingSourceStatus.Enabled);
        await SeedAsync(db, tenant, disabled.Value, AccountingSourceStatus.Disabled);
        await SeedAsync(db, tenant, syncing.Value, AccountingSourceStatus.Syncing);

        await using var ctx = db.AccountingSources();
        var activation = new EfAccountingSourceActivation(ctx, Trusted(tenant));

        Assert.True(await activation.IsActiveAsync(Tenant(tenant), enabled));
        Assert.False(await activation.IsActiveAsync(Tenant(tenant), disabled));
        // Syncing/Error are transient runtime states, not administrative refusals: they stay usable.
        Assert.True(await activation.IsActiveAsync(Tenant(tenant), syncing));
    }

    [Fact]
    public async Task Activation_fails_closed_for_missing_source_foreign_tenant_and_absent_scope()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var owner = Guid.NewGuid().ToString("N");
        var other = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedAsync(db, owner, source.Value, AccountingSourceStatus.Enabled);

        await using var ctx = db.AccountingSources();

        Assert.False(await new EfAccountingSourceActivation(ctx, Trusted(owner))
            .IsActiveAsync(Tenant(owner), Source(Guid.NewGuid().ToString("N"))));
        Assert.False(await new EfAccountingSourceActivation(ctx, Trusted(other))
            .IsActiveAsync(Tenant(owner), source));
        Assert.False(await new EfAccountingSourceActivation(ctx, DatabaseTenantScope.None)
            .IsActiveAsync(Tenant(owner), source));
    }

    // --- Command path ----------------------------------------------------------------------

    [Fact]
    public async Task Command_path_refuses_a_disabled_source_with_zero_provider_calls_and_zero_writes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedAsync(db, tenant, source.Value, AccountingSourceStatus.Disabled);

        var capability = new CountingCustomers(source);

        await using var ingestion = db.Ingestion();
        await using var accounting = db.AccountingSources();
        var scope = Trusted(tenant);
        var store = WriteStore(ingestion, scope);

        var handler = new RunCustomerSyncHandler(
            store, store, store,
            new EfAccountingSourceOwnership(accounting, scope),
            new EfAccountingSourceActivation(accounting, scope),
            new EfAccountingSourceProviderResolver(accounting, scope),
            capability);

        await Assert.ThrowsAsync<AccountingSourceDisabledException>(
            () => handler.Handle(
                new RunCustomerSyncCommand(TrustedContext(tenant), new SyncRequest(source, SyncMode.Full)),
                default));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
        Assert.Equal(0, await CountCheckpointsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Command_path_still_works_for_an_enabled_source()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedAsync(db, tenant, source.Value, AccountingSourceStatus.Enabled);

        var capability = new CountingCustomers(source, Customer(source, "C1", "fp1"), Customer(source, "C2", "fp2"));

        await using var ingestion = db.Ingestion();
        await using var accounting = db.AccountingSources();
        var scope = Trusted(tenant);
        var store = WriteStore(ingestion, scope);

        var handler = new RunCustomerSyncHandler(
            store, store, store,
            new EfAccountingSourceOwnership(accounting, scope),
            new EfAccountingSourceActivation(accounting, scope),
            new EfAccountingSourceProviderResolver(accounting, scope),
            capability);

        var result = await handler.Handle(
            new RunCustomerSyncCommand(TrustedContext(tenant), new SyncRequest(source, SyncMode.Full)),
            default);

        Assert.Equal(2, result.RecordsCreated);
        Assert.Equal(1, capability.Calls);
        Assert.Equal(2, await CountRecordsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Missing_or_foreign_source_is_denied_by_ownership_before_activation_is_consulted()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var owner = Guid.NewGuid().ToString("N");
        var other = Guid.NewGuid().ToString("N");
        var foreign = Source(Guid.NewGuid().ToString("N"));
        await SeedAsync(db, owner, foreign.Value, AccountingSourceStatus.Enabled);

        // No source is seeded for "other": missing. The seeded source belongs to "owner": foreign.
        foreach (var (actor, target) in new[] { (other, foreign), (other, Source(Guid.NewGuid().ToString("N"))) })
        {
            var capability = new CountingCustomers(target);
            var activation = new RecordingActivation(isActive: true);

            await using var ingestion = db.Ingestion();
            await using var accounting = db.AccountingSources();
            var scope = Trusted(actor);
            var store = WriteStore(ingestion, scope);

            var handler = new RunCustomerSyncHandler(
                store, store, store,
                new EfAccountingSourceOwnership(accounting, scope),
                activation,
                new EfAccountingSourceProviderResolver(accounting, scope),
                capability);

            await Assert.ThrowsAsync<UnauthorizedSourceException>(
                () => handler.Handle(
                    new RunCustomerSyncCommand(TrustedContext(actor), new SyncRequest(target, SyncMode.Full)),
                    default));

            // Ownership is decided first: activation is never consulted, and nothing is called/written.
            Assert.Equal(0, activation.Calls);
            Assert.Equal(0, capability.Calls);
            Assert.Equal(0, await CountRecordsAsync(db, actor, target));
        }
    }

    // --- Query (read-only preview) path ----------------------------------------------------

    [Fact]
    public async Task Query_path_refuses_a_disabled_source_with_zero_provider_calls_and_no_writes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedAsync(db, tenant, source.Value, AccountingSourceStatus.Disabled);

        var capability = new CountingCustomers(source);

        await using var accounting = db.AccountingSources();
        var scope = Trusted(tenant);
        var handler = new SynchronizeCustomersHandler(
            capability,
            new EfAccountingSourceOwnership(accounting, scope),
            new EfAccountingSourceActivation(accounting, scope));

        await Assert.ThrowsAsync<AccountingSourceDisabledException>(
            () => handler.Handle(
                new SynchronizeCustomersQuery(TrustedContext(tenant), new SyncRequest(source, SyncMode.Full)),
                default));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Query_path_still_returns_one_bounded_page_for_an_enabled_source()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedAsync(db, tenant, source.Value, AccountingSourceStatus.Enabled);

        var capability = new CountingCustomers(source, Customer(source, "C1", "fp1"), Customer(source, "C2", "fp2"));

        await using var accounting = db.AccountingSources();
        var scope = Trusted(tenant);
        var handler = new SynchronizeCustomersHandler(
            capability,
            new EfAccountingSourceOwnership(accounting, scope),
            new EfAccountingSourceActivation(accounting, scope));

        var page = await handler.Handle(
            new SynchronizeCustomersQuery(TrustedContext(tenant), new SyncRequest(source, SyncMode.Full)),
            default);

        Assert.Equal(2, page.Records.Count);
        Assert.Equal(1, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
    }

    // --- Test doubles ----------------------------------------------------------------------

    private sealed class CountingCustomers : ISyncablePort<SourceCustomer>
    {
        private readonly SyncBatch<SourceCustomer> _page;
        private int _calls;

        public CountingCustomers(AccountingSourceId source, params SourceCustomer[] records)
            => _page = SyncBatch<SourceCustomer>.Complete(records);

        public int Calls => Volatile.Read(ref _calls);

        public string CapabilityName => "Customers";

        public SyncModeSupport SyncModeSupport => SyncModeSupport.FullAndIncremental;

        public Task<SyncBatch<SourceCustomer>> SyncAsync(
            SyncRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(_page);
        }
    }

    private sealed class RecordingActivation : IAccountingSourceActivation
    {
        private readonly bool _isActive;
        private int _calls;

        public RecordingActivation(bool isActive) => _isActive = isActive;

        public int Calls => Volatile.Read(ref _calls);

        public Task<bool> IsActiveAsync(
            TenantId tenantId,
            AccountingSourceId sourceId,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(_isActive);
        }
    }
}

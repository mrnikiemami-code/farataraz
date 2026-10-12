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
using Xunit;

/// <summary>
/// Part B (task M5/R3): durable terminal checkpoint completion and completion-aware resume.
/// </summary>
public sealed partial class RunCustomerSyncHandlerTests
{
    [Fact]
    public async Task Terminal_page_durably_completes_the_checkpoint()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
            .SaveAsync(Tenant(tenant), source, "Customers", "c1", DateTime.UtcNow, observedVersion: null);

        var capability = new FakeCustomers((_, _) => Task.FromResult(Complete(source)));
        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        var result = await RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full));

        Assert.True(result.IsComplete);
        Assert.Equal("c1", await ReadCursorAsync(db, tenant, source));      // retained token
        Assert.Equal("Completed", await ReadStateAsync(db, tenant, source)); // durable non-resumable marker
    }

    private static async Task SeedCompletedCheckpointAsync(
        IntegrationTestDb db, string tenant, AccountingSourceId source, string cursor)
    {
        await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
            .SaveAsync(
                Tenant(tenant), source, "Customers", cursor, DateTime.UtcNow,
                observedVersion: null, state: SyncCheckpointState.Completed);
    }

    [Fact]
    public async Task Completed_checkpoint_rejects_a_normal_invocation_before_provider_and_writes()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedCompletedCheckpointAsync(db, tenant, source, "c1");

        var capability = new FakeCustomers((_, _) => Task.FromResult(Continue(source, "next", Customer(source, "C1", "fp1"))));
        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAsync<SyncAlreadyCompletedException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full)));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("Completed", await ReadStateAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Completed_checkpoint_rejects_a_supplied_historical_cursor()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedCompletedCheckpointAsync(db, tenant, source, "c1");

        var capability = new FakeCustomers((_, _) => Task.FromResult(Continue(source, "next", Customer(source, "C1", "fp1"))));
        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        var historical = new SyncCursor("c1", new SyncCursorScope(source, "Customers"));
        await Assert.ThrowsAsync<SyncAlreadyCompletedException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full, historical)));

        Assert.Equal(0, capability.Calls);
        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Explicit_restart_after_completion_requests_a_fresh_page_and_transitions_by_cas()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedCompletedCheckpointAsync(db, tenant, source, "c1");

        var capability = new FakeCustomers((_, _) => Task.FromResult(Continue(source, "fresh-cursor", Customer(source, "C1", "fp1"))));
        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full), CustomerSyncIntent.Restart);

        Assert.Null(capability.LastRequest!.Cursor);                       // no historical cursor reuse
        Assert.Equal("fresh-cursor", await ReadCursorAsync(db, tenant, source));
        Assert.Equal("Active", await ReadStateAsync(db, tenant, source));  // CAS transition, not delete/reset
        Assert.Equal(1, await CountRecordsAsync(db, tenant, source));
    }

    [Fact]
    public async Task Explicit_restart_must_not_supply_a_cursor()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedCompletedCheckpointAsync(db, tenant, source, "c1");

        var capability = new FakeCustomers((_, _) => Task.FromResult(Continue(source, "next", Customer(source, "C1", "fp1"))));
        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        var cursor = new SyncCursor("c1", new SyncCursorScope(source, "Customers"));
        await Assert.ThrowsAsync<InvalidSyncCursorException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full, cursor), CustomerSyncIntent.Restart));

        Assert.Equal(0, capability.Calls);
    }

    [Fact]
    public async Task Stale_explicit_restart_is_rejected_and_rolled_back()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));
        await SeedCompletedCheckpointAsync(db, tenant, source, "c1");

        // A concurrent writer advances the checkpoint after the engine observed version 0.
        var capability = new FakeCustomers(async (_, _) =>
        {
            await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
                .SaveAsync(Tenant(tenant), source, "Customers", "concurrent", DateTime.UtcNow, observedVersion: 0L);
            return Continue(source, "restart-cursor", Customer(source, "C1", "fp1"));
        });

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAsync<StaleCheckpointException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full), CustomerSyncIntent.Restart));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("concurrent", await ReadCursorAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

    [Fact]
    public void Inconsistent_terminal_and_nonterminal_pages_are_unrepresentable_by_the_batch_contract()
    {
        var source = Source("s");
        var record = Customer(source, "C1", "fp1");

        // A terminal page can never carry a continuation cursor, and a non-terminal page always
        // carries one — the SyncBatch factories enforce this by construction, so an inconsistent
        // page can never reach the handler's defensive validation.
        Assert.True(SyncBatch<SourceCustomer>.Complete(new[] { record }).IsComplete);
        Assert.Null(SyncBatch<SourceCustomer>.Complete(new[] { record }).NextCursor);
        Assert.False(SyncBatch<SourceCustomer>.Continue(new[] { record }, new SyncCursor("t", new SyncCursorScope(source, "Customers"))).IsComplete);
        Assert.NotNull(SyncBatch<SourceCustomer>.Continue(new[] { record }, new SyncCursor("t", new SyncCursorScope(source, "Customers"))).NextCursor);
    }

    [Fact]
    public async Task Stale_terminal_writer_is_rejected_and_rolls_back()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
            .SaveAsync(Tenant(tenant), source, "Customers", "c1", DateTime.UtcNow, observedVersion: null);

        // A concurrent writer advances the checkpoint after the engine observed version 0.
        var capability = new FakeCustomers(async (_, _) =>
        {
            await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
                .SaveAsync(Tenant(tenant), source, "Customers", "adv", DateTime.UtcNow, observedVersion: 0L);
            return Complete(source, Customer(source, "C1", "fp1"));
        });

        await using var ctx = db.Ingestion();
        var handler = Handler(ctx, tenant, FakeOwnership.OwnsAll(), capability);

        await Assert.ThrowsAsync<StaleCheckpointException>(
            () => RunAsync(handler, tenant, new SyncRequest(source, SyncMode.Full)));

        Assert.Equal(0, await CountRecordsAsync(db, tenant, source));
        Assert.Equal("adv", await ReadCursorAsync(db, tenant, source));       // not overwritten by the terminal write
        Assert.Equal("Active", await ReadStateAsync(db, tenant, source));
        Assert.Equal(0, await CountRunsAsync(db, tenant, source));
    }

}

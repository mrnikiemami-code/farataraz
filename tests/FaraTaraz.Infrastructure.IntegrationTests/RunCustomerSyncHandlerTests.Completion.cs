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

    [Fact]
    public async Task Completed_checkpoint_is_not_implicitly_replayed_and_a_new_sync_starts_fresh()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Source(Guid.NewGuid().ToString("N"));

        // First: a terminal page durably completes with cursor "c1".
        await new SyncCheckpointRepository(db.Ingestion(), Scope(tenant))
            .SaveAsync(Tenant(tenant), source, "Customers", "c1", DateTime.UtcNow, observedVersion: null);

        await using (var ctx1 = db.Ingestion())
        {
            await RunAsync(
                Handler(ctx1, tenant, FakeOwnership.OwnsAll(), new FakeCustomers((_, _) => Task.FromResult(Complete(source)))),
                tenant, new SyncRequest(source, SyncMode.Full));
        }
        Assert.Equal("Completed", await ReadStateAsync(db, tenant, source));

        // Second: a cursor-less invocation must NOT replay the historical cursor; it starts a fresh
        // synchronization and the completed marker is replaced by a new active continuation.
        var capability = new FakeCustomers((_, _) => Task.FromResult(Continue(source, "fresh-cursor", Customer(source, "C1", "fp1"))));
        await using (var ctx2 = db.Ingestion())
        {
            await RunAsync(Handler(ctx2, tenant, FakeOwnership.OwnsAll(), capability), tenant, new SyncRequest(source, SyncMode.Full));
        }

        Assert.Null(capability.LastRequest!.Cursor);
        Assert.Equal("fresh-cursor", await ReadCursorAsync(db, tenant, source));
        Assert.Equal("Active", await ReadStateAsync(db, tenant, source));
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

namespace FaraTaraz.Infrastructure.IntegrationTests;

using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Deterministic, public-registry PostgreSQL regression tests for
/// <see cref="SyncCheckpointRepository.SaveAsync"/> observed-version contract (FT-DATA-001).
///
/// Every case drives the real database through the public repository API (<c>GetAsync</c> /
/// <c>SaveAsync</c>) with independent <c>DbContext</c> instances per writer, so it exercises
/// genuine relational concurrency — never EF Core InMemory and never handwritten SQL.
///
/// The invariant under test: a save carries the version the caller observed when it read the
/// checkpoint. A writer whose observation is older than the committed version is rejected
/// (<see cref="StaleCheckpointException"/>), never overwriting a newer checkpoint with stale
/// progress; the absent-row first-write inserts version 0; and two racing first writers do not
/// crash — exactly one wins and the other is stale.
/// </summary>
public sealed class SyncCheckpointConcurrencyTests
{
    private static TenantId Tenant(string value) => new(value);
    private static AccountingSourceId Source(string value) => new(value);

    private static DatabaseTenantScope Scope(string tenant)
        => DatabaseTenantScope.FromTrusted(
            TenantContext.FromAuthenticatedPrincipal(Tenant(tenant)));

    private static SyncCheckpointRepository Repo(IntegrationTestDb db, string tenant)
        => new(db.Ingestion(), Scope(tenant));

    [Fact]
    public async Task Baseline_writer_observes_absent_row_and_inserts_version_zero()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");

        // The caller observed no row (null) -> the first write inserts version 0.
        await Repo(db, tenant).SaveAsync(
            Tenant(tenant), Source(source), "Customers", "baseline", DateTime.UtcNow, observedVersion: null);

        var version = await ReadVersionAsync(db, tenant, source, "Customers");
        Assert.Equal(0L, version);
    }

    [Fact]
    public async Task Sequential_writes_advance_observed_version_monotonically()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");

        // First write observes null -> version 0; each later write observes the previous version.
        await Repo(db, tenant).SaveAsync(
            Tenant(tenant), Source(source), "Customers", "v0", DateTime.UtcNow, observedVersion: null);

        var observedV0 = (await Repo(db, tenant).GetAsync(Tenant(tenant), Source(source), "Customers"))!.Version;
        Assert.Equal(0L, observedV0);
        await Repo(db, tenant).SaveAsync(
            Tenant(tenant), Source(source), "Customers", "v1", DateTime.UtcNow, observedVersion: observedV0);

        var observedV1 = (await Repo(db, tenant).GetAsync(Tenant(tenant), Source(source), "Customers"))!.Version;
        Assert.Equal(1L, observedV1);
        await Repo(db, tenant).SaveAsync(
            Tenant(tenant), Source(source), "Customers", "v2", DateTime.UtcNow, observedVersion: observedV1);

        var version = await ReadVersionAsync(db, tenant, source, "Customers");
        Assert.Equal(2L, version);
    }

    [Fact]
    public async Task Delayed_stale_save_with_old_observation_is_rejected()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");

        // Baseline: an absent row, so the first writer observes null and inserts version 0.
        await Repo(db, tenant).SaveAsync(
            Tenant(tenant), Source(source), "Customers", "baseline", DateTime.UtcNow, observedVersion: null);

        // Two operations both read the same baseline observation.
        var staleObserver = await Repo(db, tenant).GetAsync(Tenant(tenant), Source(source), "Customers");
        var newerObserver = await Repo(db, tenant).GetAsync(Tenant(tenant), Source(source), "Customers");
        Assert.Equal(staleObserver!.Version, newerObserver!.Version);
        Assert.Equal(0L, newerObserver!.Version);

        // The newer operation commits its observation (0 -> 1) and wins.
        await Repo(db, tenant).SaveAsync(
            Tenant(tenant), Source(source), "Customers", "newer", DateTime.UtcNow, observedVersion: newerObserver.Version);

        // The delayed operation still carries the old observation (0) and is rejected. The stale
        // cursor is never accepted; the persisted state is the newer value/version 1.
        await Assert.ThrowsAnyAsync<StaleCheckpointException>(
            () => Repo(db, tenant).SaveAsync(
                Tenant(tenant), Source(source), "Customers", "stale", DateTime.UtcNow, observedVersion: staleObserver.Version));

        var stored = await ReadCursorAsync(db, tenant, source, "Customers");
        var version = await ReadVersionAsync(db, tenant, source, "Customers");
        Assert.Equal("newer", stored);
        Assert.Equal(1L, version);
    }

    [Fact]
    public async Task Two_first_writers_observing_absent_row_one_wins_one_is_stale()
    {
        var db = new IntegrationTestDb();
        await db.ApplyMigrationsAsync();

        var tenant = Guid.NewGuid().ToString("N");
        var source = Guid.NewGuid().ToString("N");

        // Two operations both observe an absent row (null) and race. Exactly one inserts version 0;
        // the other is rejected as stale. Which cursor wins is nondeterministic, but the shape is
        // deterministic: one success, one stale, one row, version 0.
        var outcomes = await Task.WhenAll(
            SaveAbsentAsync(db, tenant, source, "alpha"),
            SaveAbsentAsync(db, tenant, source, "beta"));

        var successes = outcomes.Count(x => x.Success);
        var stale = outcomes.Count(x => x.Stale);
        Assert.Equal(1, successes);
        Assert.Equal(1, stale);

        var count = await CountAsync(db, tenant, source, "Customers");
        var version = await ReadVersionAsync(db, tenant, source, "Customers");
        Assert.Equal(1, count);
        Assert.Equal(0L, version);
    }

    private static async Task<(bool Success, bool Stale)> SaveAbsentAsync(
        IntegrationTestDb db, string tenant, string source, string cursor)
    {
        await using var ctx = db.Ingestion();
        var repo = new SyncCheckpointRepository(ctx, Scope(tenant));
        try
        {
            await repo.SaveAsync(
                Tenant(tenant), Source(source), "Customers", cursor, DateTime.UtcNow, observedVersion: null)
                .ConfigureAwait(false);
            return (true, false); // inserted version 0 (won the absent-row race)
        }
        catch (StaleCheckpointException)
        {
            return (false, true); // lost the race; rejected by the DB-enforced guard
        }
    }

    private static async Task<string?> ReadCursorAsync(
        IntegrationTestDb db, string tenant, string source, string capability)
    {
        await using var ctx = db.Ingestion();
        var entity = await ctx.SyncCheckpoints
            .FirstOrDefaultAsync(
                c => c.TenantId == tenant &&
                     c.SourceId == source &&
                     c.Capability == capability)
            .ConfigureAwait(false);
        return entity?.CursorToken;
    }

    private static async Task<long> ReadVersionAsync(
        IntegrationTestDb db, string tenant, string source, string capability)
    {
        await using var ctx = db.Ingestion();
        var entity = await ctx.SyncCheckpoints
            .FirstOrDefaultAsync(
                c => c.TenantId == tenant &&
                     c.SourceId == source &&
                     c.Capability == capability)
            .ConfigureAwait(false);
        return entity?.Version ?? -1L;
    }

    private static async Task<int> CountAsync(
        IntegrationTestDb db, string tenant, string source, string capability)
    {
        await using var ctx = db.Ingestion();
        return await ctx.SyncCheckpoints
            .CountAsync(
                c => c.TenantId == tenant &&
                     c.SourceId == source &&
                     c.Capability == capability)
            .ConfigureAwait(false);
    }
}

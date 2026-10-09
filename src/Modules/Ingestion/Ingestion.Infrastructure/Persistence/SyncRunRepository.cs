namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Tenant-scoped access to synchronization run state.
///
/// A run is started as <c>Running</c> and finished as <c>Completed</c> / <c>Failed</c> /
/// <c>Cancelled</c>. All writes are tenant-scoped and committed atomically with the records
/// they describe (ADR-010 decision 8). This stores the failure state W3 consumes; it does not
/// itself implement retries (ADR-010 decision 11).
/// </summary>
public sealed class SyncRunRepository
{
    private readonly IngestionDbContext _db;

    /// <summary>
    /// Wraps the module unit of work for synchronization run-state writes.
    /// </summary>
    public SyncRunRepository(IngestionDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// Starts a new run (<c>Running</c>) for the trusted tenant and returns the stored entity
    /// (with its generated id).
    /// </summary>
    public async Task<SyncRunEntity> StartAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        string capability,
        string mode,
        CancellationToken cancellationToken = default)
    {
        var run = new SyncRunEntity
        {
            TenantId = tenantId.Value,
            SourceId = sourceId.Value,
            Capability = capability,
            Mode = mode,
            State = SyncRunState.Running,
            RecordsProcessed = 0,
            StartedAtUtc = DateTime.UtcNow,
            FinishedAtUtc = null,
            Error = null
        };

        _db.SyncRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return run;
    }

    /// <summary>
    /// Finishes a run, setting its terminal state, processed count, and optional error.
    /// </summary>
    public async Task FinishAsync(
        long runId,
        string state,
        long recordsProcessed,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.SyncRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return;
        }

        run.State = state;
        run.RecordsProcessed = recordsProcessed;
        run.Error = error;
        run.FinishedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Terminal run states, stored as strings (Constitution E.21).</summary>
internal static class SyncRunState
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

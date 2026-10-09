namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Tenant-scoped access to synchronization run state.
///
/// <b>Trusted tenant only.</b> Tenant authority comes only from the trusted execution context
/// (Constitution A.4/A.5). Every method resolves the trusted tenant from the
/// <see cref="DatabaseTenantScope"/> bound to the current unit of work and rejects any
/// caller-supplied <c>TenantId</c> that does not match it, so an arbitrary caller tenant can
/// never authorize a run operation. Reads and writes are scoped to that trusted tenant, and a
/// missing trusted scope fails closed (throws <c>UnauthorizedTenantException</c>) — no run is
/// ever created, finished, or modified without authoritative tenant authority.
///
/// A run is started as <c>Running</c> and finished as <c>Completed</c> / <c>Failed</c> /
/// <c>Cancelled</c>. All writes are tenant-scoped and committed atomically with the records
/// they describe (ADR-010 decision 8). This stores the failure state W3 consumes; it does not
/// itself implement retries (ADR-010 decision 11).
/// </summary>
public sealed class SyncRunRepository
{
    private readonly IngestionDbContext _db;
    private readonly DatabaseTenantScope _scope;

    /// <summary>
    /// Wraps the module unit of work and the trusted tenant scope for run-state writes.
    /// </summary>
    public SyncRunRepository(IngestionDbContext db, DatabaseTenantScope scope)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
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
        var tenant = RequireTrustedTenant();
        AssertCallerTenant(tenantId, tenant);

        var run = new SyncRunEntity
        {
            TenantId = tenant.Value,
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
    /// Finishes a run, setting its terminal state, processed count, and optional error. The run
    /// is located scoped to the trusted tenant, so a run owned by another tenant is never
    /// modified.
    /// </summary>
    public async Task FinishAsync(
        long runId,
        string state,
        long recordsProcessed,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var tenant = RequireTrustedTenant();

        var run = await _db.SyncRuns
            .FirstOrDefaultAsync(
                r => r.Id == runId && r.TenantId == tenant.Value, cancellationToken)
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

    /// <summary>
    /// Resolves the trusted tenant for this unit of work, failing closed when no trusted scope
    /// is bound.
    /// </summary>
    private TenantId RequireTrustedTenant()
    {
        if (_scope.TenantId is { } trustedTenantId)
        {
            return trustedTenantId;
        }

        throw new UnauthorizedTenantException(
            "No trusted tenant scope is bound for this unit of work.");
    }

    /// <summary>
    /// Rejects a caller-supplied tenant that does not match the trusted tenant. The trusted
    /// scope is the only basis of tenant authority; the caller tenant is validated, never trusted.
    /// </summary>
    private static void AssertCallerTenant(TenantId callerTenantId, TenantId trustedTenantId)
    {
        if (callerTenantId.Value != trustedTenantId.Value)
        {
            throw new UnauthorizedTenantException(
                "Caller tenant does not match the trusted tenant scope.");
        }
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

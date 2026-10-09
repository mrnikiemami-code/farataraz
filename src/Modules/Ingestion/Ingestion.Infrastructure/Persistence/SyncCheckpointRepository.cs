namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Tenant-scoped access to synchronization checkpoints (resumption cursors).
///
/// <b>Trusted tenant only.</b> Tenant authority comes only from the trusted execution context
/// (Constitution A.4/A.5). Every method resolves the trusted tenant from the
/// <see cref="DatabaseTenantScope"/> bound to the current unit of work, rejects any
/// caller-supplied <c>TenantId</c> that does not match it, and scopes reads and writes to that
/// trusted tenant. A missing trusted scope fails closed (throws
/// <c>UnauthorizedTenantException</c>), so a cursor from another tenant is never read or
/// overwritten.
///
/// A checkpoint is stored once per (tenant, source, capability); saving a new cursor upserts the
/// existing row (ADR-010 decision 9).
/// </summary>
public sealed class SyncCheckpointRepository
{
    private readonly IngestionDbContext _db;
    private readonly DatabaseTenantScope _scope;

    /// <summary>
    /// Wraps the module unit of work and the trusted tenant scope for checkpoint (cursor) reads
    /// and writes.
    /// </summary>
    public SyncCheckpointRepository(IngestionDbContext db, DatabaseTenantScope scope)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <summary>
    /// Returns the current checkpoint for the trusted tenant's source/capability, or
    /// <c>null</c> when none has been stored yet (a fresh sync). Cross-tenant cursors are never
    /// returned.
    /// </summary>
    public async Task<SyncCheckpointEntity?> GetAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        string capability,
        CancellationToken cancellationToken = default)
    {
        var tenant = RequireTrustedTenant();
        AssertCallerTenant(tenantId, tenant);

        return await _db.SyncCheckpoints
            .FirstOrDefaultAsync(
                c => c.TenantId == tenant.Value &&
                     c.SourceId == sourceId.Value &&
                     c.Capability == capability,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Saves (upserts) the cursor for the trusted tenant's source/capability. Existing rows are
    /// updated in place; the row is created when no checkpoint exists yet.
    /// </summary>
    public async Task SaveAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        string capability,
        string cursorToken,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var tenant = RequireTrustedTenant();
        AssertCallerTenant(tenantId, tenant);

        var existing = await GetAsync(tenantId, sourceId, capability, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.SyncCheckpoints.Add(new SyncCheckpointEntity
            {
                TenantId = tenant.Value,
                SourceId = sourceId.Value,
                Capability = capability,
                CursorToken = cursorToken,
                UpdatedAtUtc = updatedAtUtc
            });
        }
        else
        {
            existing.CursorToken = cursorToken;
            existing.UpdatedAtUtc = updatedAtUtc;
        }

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

namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Tenant-scoped access to synchronization checkpoints (resumption cursors).
///
/// A checkpoint is stored once per (tenant, source, capability); saving a new cursor upserts the
/// existing row (ADR-010 decision 9). Reads are always tenant-scoped, so a cursor from another
/// tenant is never returned.
/// </summary>
public sealed class SyncCheckpointRepository
{
    private readonly IngestionDbContext _db;

    /// <summary>
    /// Wraps the module unit of work for checkpoint (cursor) reads and writes.
    /// </summary>
    public SyncCheckpointRepository(IngestionDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// Returns the current checkpoint for the trusted tenant's source/capability, or
    /// <c>null</c> when none has been stored yet (a fresh sync).
    /// </summary>
    public async Task<SyncCheckpointEntity?> GetAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        string capability,
        CancellationToken cancellationToken = default)
    {
        return await _db.SyncCheckpoints
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId.Value &&
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
        var existing = await GetAsync(tenantId, sourceId, capability, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.SyncCheckpoints.Add(new SyncCheckpointEntity
            {
                TenantId = tenantId.Value,
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
}

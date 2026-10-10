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
    /// Saves (upserts) the cursor for the trusted tenant's source/capability using an atomic,
    /// DB-enforced optimistic-concurrency conditional upsert (FT-DATA-001).
    ///
    /// The write is a single PostgreSQL statement:
    /// <c>INSERT ... ON CONFLICT (scope) DO UPDATE SET cursor, updated_at, version = new
    /// WHERE version = @expected</c>. It is accepted only when the row's current version equals the
    /// version the writer last observed; otherwise it affects zero rows and the write is rejected
    /// (throwing <see cref="StaleCheckpointException"/>). This makes a stale writer unable to
    /// overwrite a newer checkpoint with stale progress — the newer write (a higher committed
    /// version) wins, and the stale writer is rejected and must re-read.
    ///
    /// The first write (no row yet) inserts <c>version = 0</c>; a concurrent first writer conflicts
    /// on the unique constraint and is rejected (never silently discarded, never crashes on a
    /// unique-violation). The <c>Version</c> guard is DB-managed and monotonic, so the ordering is
    /// commit order — not the opaque cursor token (not lexically ordered) and not
    /// <c>UpdatedAtUtc</c> (wall-clock, skew-prone).
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

        // The optimistic-concurrency guard is the version the writer last observed. A null means
        // "no row yet" (first writer); the guard then can never match an existing row, so a
        // concurrent first writer is rejected rather than clobbering the row it did not read.
        var existing = await GetAsync(tenantId, sourceId, capability, cancellationToken)
            .ConfigureAwait(false);
        var expectedVersion = existing?.Version ?? -1L;
        var newVersion = expectedVersion < 0L ? 0L : expectedVersion + 1L;

        var affected = await _db.Database
            .ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO ""SyncCheckpoints""
                       (""TenantId"", ""SourceId"", ""Capability"", ""CursorToken"",
                        ""UpdatedAtUtc"", ""Version"")
                       VALUES ({tenant.Value}, {sourceId.Value}, {capability}, {cursorToken},
                               {updatedAtUtc}, {newVersion})
                       ON CONFLICT (""TenantId"", ""SourceId"", ""Capability"") DO UPDATE
                       SET ""CursorToken"" = EXCLUDED.""CursorToken"",
                           ""UpdatedAtUtc"" = EXCLUDED.""UpdatedAtUtc"",
                           ""Version"" = EXCLUDED.""Version""
                       WHERE ""SyncCheckpoints"".""Version"" = {expectedVersion};",
                cancellationToken)
            .ConfigureAwait(false);

        if (affected == 0)
        {
            // A newer checkpoint was committed for this scope since the writer read it. The newer
            // write wins; the stale writer is rejected and must re-read (never overwrite a newer
            // checkpoint with stale progress — FT-DATA-001).
            var current = await GetAsync(tenantId, sourceId, capability, cancellationToken)
                .ConfigureAwait(false);

            throw new StaleCheckpointException(
                tenant.Value,
                sourceId.Value,
                capability,
                expectedVersion,
                current?.Version ?? -1L);
        }
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

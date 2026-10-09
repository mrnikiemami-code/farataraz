namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tenant-scoped write access to source records (identity + provenance).
///
/// The insert is idempotent via PostgreSQL <c>ON CONFLICT ... DO NOTHING</c> against the unique
/// idempotency key <c>(TenantId, SourceId, RecordKind, ExternalId)</c>: a duplicate delivery is
/// rejected (never creates a second row) and the first delivery's provenance is preserved
/// (ADR-010 decision 7). Concurrency is resolved by the constraint — exactly one of two racing
/// inserts wins; the other conflicts and is treated as a duplicate. Returns <c>true</c> when a
/// NEW row was created, <c>false</c> when the delivery was a duplicate.
///
/// The statement is parameterised through EF Core's <c>ExecuteSqlInterpolatedAsync</c>: every
/// interpolated value is bound as a parameter, and optional columns bind SQL NULL when their
/// value is <c>null</c> (the provider maps a null parameter to SQL NULL).
/// </summary>
public sealed class SourceRecordRepository
{
    private readonly IngestionDbContext _db;
    private readonly ILogger _logger;

    /// <summary>
    /// Wraps the module unit of work for source-record identity and provenance writes.
    /// </summary>
    public SourceRecordRepository(IngestionDbContext db, ILogger<SourceRecordRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns <c>true</c> when a source record with this identity already exists for the
    /// trusted tenant. Always tenant-scoped; cross-tenant lookups return <c>false</c>.
    /// </summary>
    public async Task<bool> ExistsAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        string recordKind,
        string externalId,
        CancellationToken cancellationToken = default)
    {
        return await _db.SourceRecords
            .AnyAsync(
                e => e.TenantId == tenantId.Value &&
                     e.SourceId == sourceId.Value &&
                     e.RecordKind == recordKind &&
                     e.ExternalId == externalId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts a source record idempotently. Returns <c>true</c> if a NEW row was created,
    /// <c>false</c> if the delivery was a duplicate (the unique idempotency key already exists).
    /// Only the VALUES are parameterised; column/constraint names are literal SQL.
    /// </summary>
    public async Task<bool> InsertOrUpdateAsync(
        TenantId tenantId,
        SourceRecordId recordId,
        SourceRecordVersion? version,
        SourceProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        var affected = await _db.Database
            .ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO ""SourceRecords""
                       (""TenantId"", ""SourceId"", ""RecordKind"", ""ExternalId"",
                        ""ContentFingerprint"", ""ProviderRevision"", ""ProviderModifiedAtUtc"",
                        ""RetrievedAtUtc"", ""Provider"", ""Checkpoint""
                       )
                       VALUES ({tenantId.Value}, {recordId.SourceId.Value}, {recordId.RecordKind}, {recordId.ExternalId},
                               {version?.ContentFingerprint}, {version?.ProviderRevision}, {version?.ProviderModifiedAtUtc},
                               {provenance.RetrievedAtUtc}, {provenance.Provider.Value}, {provenance.Checkpoint})
                       ON CONFLICT (""TenantId"", ""SourceId"", ""RecordKind"", ""ExternalId""
                       ) DO NOTHING;",
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "Idempotently inserted source record {SourceId}/{Kind}/{External} for tenant {Tenant} (created={Created})",
            recordId.SourceId, recordId.RecordKind, recordId.ExternalId, tenantId, affected > 0);

        // ON CONFLICT DO NOTHING affects 1 row on a new row and 0 on a duplicate, so the row count
        // is the created-flag (ADR-010 decision 7).
        return affected > 0;
    }
}

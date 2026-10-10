namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core context for the Ingestion module.
///
/// Owns the schema for synchronization run state, checkpoints/cursors, and source-record
/// identity/provenance. Every table is tenant-scoped; the idempotency and checkpoint uniqueness
/// constraints make duplicate delivery and duplicate cursors impossible at the database
/// (ADR-010 decisions 7–9, Constitution E.20).
///
/// This context is a unit of work resolved through DI. It contains NO business rules: it never
/// decides retries, duplicates, or freshness — it only stores and returns tenant-scoped data.
/// </summary>
public sealed class IngestionDbContext : DbContext
{
    /// <summary>
    /// Creates the module unit of work. Resolved through DI with a trusted tenant scope bound.
    /// </summary>
    public IngestionDbContext(DbContextOptions<IngestionDbContext> options)
        : base(options)
    {
    }

    /// <summary>Synchronization runs for the trusted tenant. Never null.</summary>
    public DbSet<SyncRunEntity> SyncRuns => Set<SyncRunEntity>();

    /// <summary>Synchronization checkpoints (resumption cursors) for the trusted tenant. Never null.</summary>
    public DbSet<SyncCheckpointEntity> SyncCheckpoints => Set<SyncCheckpointEntity>();

    /// <summary>Synchronized source records (identity + provenance) for the trusted tenant. Never null.</summary>
    public DbSet<SourceRecordEntity> SourceRecords => Set<SourceRecordEntity>();

    /// <summary>
    /// Configures the tenant-scoped schema and the durable idempotency/uniqueness constraints.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // --- Run state -----------------------------------------------------------------------
        builder.Entity<SyncRunEntity>(entity =>
        {
            entity.ToTable("SyncRuns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).HasMaxLength(255);
            entity.Property(e => e.SourceId).HasMaxLength(255);
            entity.Property(e => e.Capability).HasMaxLength(255);
            entity.Property(e => e.Mode).HasMaxLength(32);
            entity.Property(e => e.State).HasMaxLength(32);
            entity.Property(e => e.Error).HasMaxLength(2000);
            entity.Property(e => e.RecordsProcessed);
            entity.Property(e => e.StartedAtUtc);
            entity.Property(e => e.FinishedAtUtc);
        });

        // --- Checkpoint ----------------------------------------------------------------------
        builder.Entity<SyncCheckpointEntity>(entity =>
        {
            entity.ToTable("SyncCheckpoints");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).HasMaxLength(255);
            entity.Property(e => e.SourceId).HasMaxLength(255);
            entity.Property(e => e.Capability).HasMaxLength(255);
            entity.Property(e => e.CursorToken).HasMaxLength(1024);
            entity.Property(e => e.UpdatedAtUtc);
            entity.Property(e => e.Version);

            // One cursor per (tenant, source, capability). A duplicate resume for the same scope
            // upserts the existing row; a cursor for a different scope is a different row.
            entity.HasIndex(new[] { "TenantId", "SourceId", "Capability" })
                .HasDatabaseName("UQ_SyncCheckpoints_Tenant_Source_Capability")
                .IsUnique();
        });

        // --- Source record (idempotency key + provenance) ------------------------------------
        builder.Entity<SourceRecordEntity>(entity =>
        {
            entity.ToTable("SourceRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).HasMaxLength(255);
            entity.Property(e => e.SourceId).HasMaxLength(255);
            entity.Property(e => e.RecordKind).HasMaxLength(64);
            entity.Property(e => e.ExternalId).HasMaxLength(512);
            entity.Property(e => e.ContentFingerprint).HasMaxLength(128);
            entity.Property(e => e.ProviderRevision).HasMaxLength(255);
            entity.Property(e => e.Provider).HasMaxLength(255);
            entity.Property(e => e.Checkpoint).HasMaxLength(1024);
            entity.Property(e => e.RetrievedAtUtc);
            entity.Property(e => e.ProviderModifiedAtUtc);

            // Durable idempotency key: the same source record (same tenant + source + kind +
            // external id) is stored once. A second identical delivery is rejected by this
            // constraint, so it can never create duplicate downstream state.
            entity.HasIndex(new[] { "TenantId", "SourceId", "RecordKind", "ExternalId" })
                .HasDatabaseName("UQ_SourceRecords_Tenant_Source_Kind_External")
                .IsUnique();
        });

        base.OnModelCreating(builder);
    }
}

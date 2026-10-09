namespace FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core context for the AccountingSources module.
///
/// Owns the schema for <see cref="TenantEntity"/> and
/// <see cref="AccountingSourceEntity">AccountingSourceEntity</see>. All tenant-owned data is
/// tenant-scoped; a UNIQUE constraint on <c>AccountingSources(TenantId, Id)</c> makes
/// cross-tenant rows impossible at the database (ADR-010 decision 4, Constitution A.3).
///
/// This context is a unit of work resolved through DI. It contains NO business rules: proving
/// that the trusted Tenant owns a source is the fail-closed
/// <c>IAccountingSourceOwnership</c> oracle, not this context.
/// </summary>
public sealed class AccountingSourcesDbContext : DbContext
{
    /// <summary>
    /// Creates the module unit of work. Resolved through DI with a trusted tenant scope bound.
    /// </summary>
    public AccountingSourcesDbContext(DbContextOptions<AccountingSourcesDbContext> options)
        : base(options)
    {
    }

    /// <summary>Tenants of the platform. Never null.</summary>
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    /// <summary>Accounting sources owned by tenants. Never null.</summary>
    public DbSet<AccountingSourceEntity> AccountingSources => Set<AccountingSourceEntity>();

    /// <summary>
    /// Configures the tenant-scoped schema and the cross-tenant isolation constraint.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Entity<TenantEntity>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255);
        });

        builder.Entity<AccountingSourceEntity>(entity =>
        {
            entity.ToTable("AccountingSources");
            entity.HasKey(e => new { e.TenantId, e.Id });

            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.TenantId).HasMaxLength(255);
            entity.Property(e => e.Provider).HasMaxLength(255);
            entity.Property(e => e.DisplayName).HasMaxLength(255);
            entity.Property(e => e.Status);

            entity.HasOne(e => e.Tenant)
                .WithMany(e => e.AccountingSources)
                .IsRequired(); // an accounting source cannot exist without a tenant

            // Cross-tenant isolation + ownership: one source per (tenant, id). A source id is
            // unique to its tenant, so equal ids in different tenants are different sources.
            entity.HasIndex(new[] { "TenantId", "Id" })
                .HasDatabaseName("UQ_AccountingSources_Tenant_Id")
                .IsUnique();
        });

        base.OnModelCreating(builder);
    }
}

namespace FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;

using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;

/// <summary>
/// EF Core mapping of an <see cref="AccountingSource"/>.
///
/// One configured accounting-system instance that belongs to exactly one <see cref="Tenant"/>.
/// The row is tenant-scoped: the <see cref="TenantId"/> column is a foreign key to
/// <see cref="TenantEntity.Id"/>, and a UNIQUE constraint on
/// <c>(TenantId, Id)</c> makes cross-tenant rows impossible at the database
/// (ADR-010 decision 4, Constitution A.3).
/// </summary>
public sealed class AccountingSourceEntity
{
    /// <summary>Platform-owned identity of this accounting source (<see cref="AccountingSourceId.Value"/>).</summary>
    public string Id { get; set; } = default!;

    /// <summary>The single tenant this source belongs to (foreign key).</summary>
    public string TenantId { get; set; } = default!;

    /// <summary>Owns this source. Never null.</summary>
    public TenantEntity Tenant { get; set; } = default!;

    /// <summary>The provider identity (e.g. Asan / Sepidar / Mock). Not a provider DTO.</summary>
    public string Provider { get; set; } = default!;

    /// <summary>Human-readable name used by operators.</summary>
    public string DisplayName { get; set; } = default!;

    /// <summary>Operational status of the source (<see cref="AccountingSourceStatus"/>).</summary>
    public int Status { get; set; }
}

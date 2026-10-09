namespace FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;

using System.Collections.ObjectModel;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;

/// <summary>
/// EF Core mapping of a <see cref="Tenant"/>.
///
/// A tenant is the purchasing organization that consumes FaraTaraz. It is NOT an accounting
/// system, branch, or customer. The strong <see cref="TenantId"/> struct is not EF-mappable,
/// so the platform-owned id is stored as its string value in the <c>Id</c> column.
/// </summary>
public sealed class TenantEntity
{
    /// <summary>Platform-owned tenant id (the <see cref="TenantId.Value"/>).</summary>
    public string Id { get; set; } = default!;

    /// <summary>Human-readable display name of the purchasing organization.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Tenant-owned accounting sources. Never null.</summary>
    public ICollection<AccountingSourceEntity> AccountingSources { get; set; } =
        new Collection<AccountingSourceEntity>();
}

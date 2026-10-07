namespace FaraTaraz.BuildingBlocks.Tenancy;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// A <see cref="Tenant"/> is the purchasing organization that consumes FaraTaraz.
///
/// A Tenant is NOT:
/// - an accounting system
/// - an accounting branch
/// - a customer stored in an accounting system
///
/// Every piece of tenant-owned business data references a <see cref="TenantId"/>.
/// </summary>
public sealed class Tenant
{
    public Tenant(TenantId id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>Platform-owned identity of the tenant.</summary>
    public TenantId Id { get; }

    /// <summary>Human-readable display name of the purchasing organization.</summary>
    public string Name { get; }

    public static Tenant Create(TenantId id, string name) => new(id, name);
}

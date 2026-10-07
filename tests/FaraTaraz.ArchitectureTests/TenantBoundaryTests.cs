namespace FaraTaraz.ArchitectureTests;

using System.Linq;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using Xunit;

/// <summary>
/// Tenant ownership is enforced at the type level: an AccountingSource cannot be
/// constructed without a TenantId, and is always exposed with a TenantId property.
/// </summary>
public class TenantBoundaryTests
{
    [Fact]
    public void AccountingSource_requires_tenant_identity_in_every_public_constructor()
    {
        foreach (var ctor in typeof(AccountingSource).GetConstructors())
        {
            Assert.True(
                ctor.GetParameters().Any(p => p.ParameterType == typeof(TenantId)),
                "Every AccountingSource constructor must require a TenantId.");
        }
    }

    [Fact]
    public void AccountingSource_exposes_tenant_identity_of_type_TenantId()
    {
        var prop = typeof(AccountingSource).GetProperty(nameof(AccountingSource.TenantId));

        Assert.NotNull(prop);
        Assert.Equal(typeof(TenantId), prop!.PropertyType);
    }
}

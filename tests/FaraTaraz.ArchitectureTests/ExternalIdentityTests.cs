namespace FaraTaraz.ArchitectureTests;

using System.Linq;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.MasterData;
using Xunit;

/// <summary>
/// External identities are scoped to their accounting source; canonical identities
/// are platform-owned and tenant-bound. Equal external codes across sources do NOT
/// prove equal canonical entities (that rule is deliberately not encoded here).
/// </summary>
public class ExternalIdentityTests
{
    [Fact]
    public void External_customer_identity_is_scoped_to_accounting_source()
    {
        var sourceField = typeof(ExternalCustomerId)
            .GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(AccountingSourceId));

        Assert.NotNull(sourceField);
    }

    [Fact]
    public void External_product_identity_is_scoped_to_accounting_source()
    {
        var sourceField = typeof(ExternalProductId)
            .GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(AccountingSourceId));

        Assert.NotNull(sourceField);
    }

    [Fact]
    public void Canonical_customer_is_platform_owned_and_tenant_bound()
    {
        var tenantField = typeof(CanonicalCustomer)
            .GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(TenantId));

        Assert.NotNull(tenantField);
    }

    [Fact]
    public void Canonical_product_is_platform_owned_and_tenant_bound()
    {
        var tenantField = typeof(CanonicalProduct)
            .GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(TenantId));

        Assert.NotNull(tenantField);
    }
}

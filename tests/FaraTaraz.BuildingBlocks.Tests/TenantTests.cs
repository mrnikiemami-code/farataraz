namespace FaraTaraz.BuildingBlocks.Tests;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using Xunit;

public class TenantTests
{
    [Fact]
    public void Tenant_is_a_distinct_concept_from_AccountingSource()
    {
        var tenantId = new TenantId("tenant-1");
        var sourceId = new AccountingSourceId("src-1");

        var tenant = Tenant.Create(tenantId, "Acme Corp");

        Assert.Equal(tenantId, tenant.Id);
        Assert.NotEqual(new TenantId(sourceId.Value), tenantId);
    }

    [Fact]
    public void AccountingSource_requires_a_tenant_identity()
    {
        var source = AccountingSource.Create(
            new AccountingSourceId("src-1"),
            new TenantId("tenant-1"),
            new ProviderId("Asan"),
            "Asan Tehran",
            AccountingSourceStatus.Enabled);

        Assert.Equal(new TenantId("tenant-1"), source.TenantId);
        Assert.Equal(new AccountingSourceId("src-1"), source.Id);
    }

    [Fact]
    public void One_tenant_may_own_many_accounting_sources()
    {
        var tenantId = new TenantId("tenant-1");

        var a = AccountingSource.Create(new AccountingSourceId("a"), tenantId, new ProviderId("Asan"), "A", AccountingSourceStatus.Enabled);
        var b = AccountingSource.Create(new AccountingSourceId("b"), tenantId, new ProviderId("Sepidar"), "B", AccountingSourceStatus.Enabled);

        Assert.Equal(tenantId, a.TenantId);
        Assert.Equal(tenantId, b.TenantId);
        Assert.NotEqual(a.Id, b.Id);
    }
}

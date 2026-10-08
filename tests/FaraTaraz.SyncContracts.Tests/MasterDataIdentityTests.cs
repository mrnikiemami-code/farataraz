namespace FaraTaraz.SyncContracts.Tests;

using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.MasterData;
using Xunit;

public sealed class MasterDataIdentityTests
{
    [Fact]
    public void External_identity_is_scoped_to_source_and_rejects_missing_values()
    {
        var first = new ExternalCustomerId(new AccountingSourceId("source-a"), "001");
        var second = new ExternalCustomerId(new AccountingSourceId("source-b"), "001");
        Assert.NotEqual(first, second);
        Assert.Throws<ArgumentException>(() => new ExternalCustomerId(new AccountingSourceId("source-a"), " "));
        Assert.Throws<ArgumentException>(() => new ExternalProductId(new AccountingSourceId(""), "001"));
    }

    [Fact]
    public void Canonical_identity_is_platform_owned_and_tenant_bound()
    {
        var id = new CanonicalId("canonical-001");
        Assert.NotEqual(new CanonicalCustomer(new TenantId("tenant-a"), id),
            new CanonicalCustomer(new TenantId("tenant-b"), id));
        Assert.Throws<ArgumentException>(() => new CanonicalId(" "));
    }
}

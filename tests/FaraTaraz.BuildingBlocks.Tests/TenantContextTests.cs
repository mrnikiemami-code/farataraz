namespace FaraTaraz.BuildingBlocks.Tests;

using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using Xunit;

public class TenantContextTests
{
    [Fact]
    public void Trusted_context_is_established_only_by_authenticated_principal()
    {
        var context = TenantContext.FromAuthenticatedPrincipal(new TenantId("tenant-1"));

        Assert.True(context.IsTrusted);
        context.AssertTrusted(); // must not throw
    }

    [Fact]
    public void Client_input_is_not_a_trusted_tenant_authority()
    {
        var context = new TenantContext(new TenantId("tenant-999"), TenantContextOrigin.ClientInput);

        Assert.False(context.IsTrusted);
        Assert.Throws<UnauthorizedTenantException>(context.AssertTrusted);
    }

    [Fact]
    public void Unknown_origin_is_not_trusted()
    {
        var context = new TenantContext(new TenantId("tenant-999"), TenantContextOrigin.Unknown);

        Assert.False(context.IsTrusted);
        Assert.Throws<UnauthorizedTenantException>(context.AssertTrusted);
    }
}

namespace FaraTaraz.BuildingBlocks.Tests;

using System.Reflection;
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

    // FT-SEC-004 negative guard: untrusted code (any assembly other than the trusted
    // foundation) must NOT be able to create authoritative tenant authority. The trusted
    // minting entry points are internal, so the PUBLIC API surface exposes none of them.
    [Fact]
    public void Untrusted_code_cannot_mint_a_trusted_tenant_context()
    {
        // No public factory can mint a trusted context from an arbitrary TenantId.
        var mintingFactory = typeof(TenantContext).GetMethod(
            nameof(TenantContext.FromAuthenticatedPrincipal),
            BindingFlags.Public | BindingFlags.Static);

        Assert.Null(mintingFactory);

        // No public constructor may yield a trusted context.
        var publicConstructors = typeof(TenantContext)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Empty(publicConstructors);
    }
}

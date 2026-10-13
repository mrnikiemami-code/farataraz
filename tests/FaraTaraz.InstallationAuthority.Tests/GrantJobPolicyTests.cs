namespace FaraTaraz.InstallationAuthority.Tests;

using System;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Stage-B job-eligibility tests. Every case starts from otherwise-valid C1 claims and changes one
/// condition, so the returned failure is attributable to that condition alone.
/// </summary>
public sealed class GrantJobPolicyTests
{
    private static readonly DateTimeOffset Valid = DateTimeOffset.FromUnixTimeSeconds(1500);

    private static GrantJobEligibilityFailure Evaluate(
        ValidatedGrantClaims claims, GrantJobRequest request, DateTimeOffset? at = null)
        => GrantJobPolicy.Evaluate(claims, request, TestGrantFactory.Expectations(), at ?? Valid);

    [Fact]
    public void Eligible_job_returns_none()
    {
        var (claims, installation) = TestGrantFactory.Create();
        using (installation)
        {
            var request = new GrantJobRequest("install-1", new AccountingSourceId("src-1"), "Customers");

            Assert.Equal(GrantJobEligibilityFailure.None, Evaluate(claims, request));
        }
    }

    [Fact]
    public void Wrong_issuer_or_audience_is_rejected()
    {
        var (wrongIssuer, i1) = TestGrantFactory.Create(issuer: "someone-else");
        using (i1)
        {
            Assert.Equal(GrantJobEligibilityFailure.IssuerMismatch,
                Evaluate(wrongIssuer, new GrantJobRequest("install-1", new AccountingSourceId("src-1"), "Customers")));
        }

        var (wrongAudience, i2) = TestGrantFactory.Create(audience: "other-app");
        using (i2)
        {
            Assert.Equal(GrantJobEligibilityFailure.AudienceMismatch,
                Evaluate(wrongAudience, new GrantJobRequest("install-1", new AccountingSourceId("src-1"), "Customers")));
        }
    }

    [Fact]
    public void Wrong_installation_identity_is_rejected()
    {
        var (claims, installation) = TestGrantFactory.Create(installationId: "install-1");
        using (installation)
        {
            Assert.Equal(GrantJobEligibilityFailure.InstallationMismatch,
                Evaluate(claims, new GrantJobRequest("install-2", new AccountingSourceId("src-1"), "Customers")));
        }
    }

    [Theory]
    [InlineData(999, 4)]    // before nbf -> NotYetValid
    [InlineData(1000, 0)]   // exactly nbf -> eligible
    [InlineData(1999, 0)]   // just before exp -> eligible
    [InlineData(2000, 5)]   // exactly exp -> Expired
    public void Boundary_times_are_evaluated_exactly(long unixSeconds, int expected)
    {
        var expectedFailure = (GrantJobEligibilityFailure)expected;
        var (claims, installation) = TestGrantFactory.Create(nbf: 1000, exp: 2000);
        using (installation)
        {
            var request = new GrantJobRequest("install-1", new AccountingSourceId("src-1"), "Customers");

            Assert.Equal(expectedFailure, Evaluate(claims, request, DateTimeOffset.FromUnixTimeSeconds(unixSeconds)));
        }
    }

    [Fact]
    public void Non_granted_source_or_capability_is_rejected()
    {
        var (claims, installation) = TestGrantFactory.Create(sourceId: "src-1");
        using (installation)
        {
            // Same tenant, owned source — but not the granted source.
            Assert.Equal(GrantJobEligibilityFailure.ScopeNotGranted,
                Evaluate(claims, new GrantJobRequest("install-1", new AccountingSourceId("src-2"), "Customers")));

            // Granted source, capability not listed.
            Assert.Equal(GrantJobEligibilityFailure.ScopeNotGranted,
                Evaluate(claims, new GrantJobRequest("install-1", new AccountingSourceId("src-1"), "Inventory")));

            // Ordinal comparison: no case folding or normalization.
            Assert.Equal(GrantJobEligibilityFailure.ScopeNotGranted,
                Evaluate(claims, new GrantJobRequest("install-1", new AccountingSourceId("src-1"), "customers")));
        }
    }

    [Fact]
    public void Request_construction_rejects_empty_values()
    {
        Assert.Throws<ArgumentException>(() => new GrantJobRequest(" ", new AccountingSourceId("src-1"), "Customers"));
        Assert.Throws<ArgumentException>(() => new GrantJobRequest("install-1", new AccountingSourceId("  "), "Customers"));
        Assert.Throws<ArgumentException>(() => new GrantJobRequest("install-1", new AccountingSourceId("src-1"), "  "));
    }

    [Fact]
    public void Expectations_construction_rejects_empty_values()
    {
        Assert.Throws<ArgumentException>(() => new GrantAuthorityExpectations(" ", "farataraz-worker"));
        Assert.Throws<ArgumentException>(() => new GrantAuthorityExpectations("farataraz", " "));
    }
}

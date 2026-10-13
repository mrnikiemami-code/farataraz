namespace FaraTaraz.InstallationAuthority.Tests;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Direct constructor-input defensive-copy coverage (C1-TEST-001): the trust store must copy key
/// arrays and the claims type must deep-copy nested collections, so a caller mutating its own
/// arrays or lists after construction can never alter verified security data.
/// </summary>
public sealed class GrantTypeImmutabilityTests
{
    [Fact]
    public void Trust_store_copies_key_arrays_so_later_mutation_has_no_effect()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var spki = company.ExportSubjectPublicKeyInfo();
        var keys = TrustedAuthorityKeySet.Create(new[]
        {
            new KeyValuePair<string, byte[]>(TestGrantSigner.KeyId, spki),
        });

        var grant = TestGrantSigner.Sign(company, TestGrantSigner.Payload("AA"));

        // Destroy the caller-owned array AFTER construction.
        Array.Clear(spki, 0, spki.Length);

        // The stored copy is intact, so the key still resolves and the signature still verifies.
        Assert.Equal(GrantVerificationFailure.InvalidClaims,
            GrantVerifier.Verify(grant, keys, default).Failure);
    }

    [Fact]
    public void Trust_store_rejects_empty_and_duplicate_key_ids()
    {
        using var company = TestGrantSigner.NewCompanyKey();
        var spki = company.ExportSubjectPublicKeyInfo();

        Assert.Throws<ArgumentException>(() => TrustedAuthorityKeySet.Create(new[]
        {
            new KeyValuePair<string, byte[]>("  ", spki),
        }));

        Assert.Throws<ArgumentException>(() => TrustedAuthorityKeySet.Create(new[]
        {
            new KeyValuePair<string, byte[]>("k", spki),
            new KeyValuePair<string, byte[]>("k", spki),
        }));
    }

    [Fact]
    public void Claims_deep_copy_nested_collections_and_ignore_later_mutation()
    {
        using var installation = TestGrantSigner.NewCompanyKey();
        var spki = installation.ExportSubjectPublicKeyInfo();

        var sourceIds = new List<string> { "src-1", "src-2" };
        var capabilities = new List<string[]> { new[] { "Customers" }, new[] { "Products", "Inventory" } };

        var claims = new ValidatedGrantClaims(
            "farataraz", "farataraz-worker", "grant-1", "install-1", new TenantId("tenant-1"), "kid-1",
            1000, 1000, 2000, 30, spki, sourceIds, capabilities);

        // Mutate every caller-owned collection AFTER construction.
        sourceIds[0] = "tampered";
        sourceIds.Add("extra");
        capabilities[0][0] = "tampered";
        Array.Clear(spki, 0, spki.Length);

        Assert.Equal(2, claims.ScopeCount);
        Assert.Equal("src-1", claims.ScopeSourceId(0).Value);
        Assert.Equal("src-2", claims.ScopeSourceId(1).Value);
        Assert.Equal("Customers", claims.Capability(0, 0));
        Assert.Equal(2, claims.CapabilityCount(1));
        Assert.Equal("Products", claims.Capability(1, 0));
        Assert.Equal("Inventory", claims.Capability(1, 1));

        // The installation SPKI is likewise a private copy.
        var copy = claims.CopyInstallationPublicKeySpki();
        using var probe = ECDsa.Create();
        probe.ImportSubjectPublicKeyInfo(copy, out _);
        Assert.Equal(256, probe.KeySize);
    }

    [Fact]
    public void Claims_construction_rejects_mismatched_or_null_collections()
    {
        using var installation = TestGrantSigner.NewCompanyKey();
        var spki = installation.ExportSubjectPublicKeyInfo();

        Assert.Throws<ArgumentException>(() => new ValidatedGrantClaims(
            "i", "a", "g", "n", new TenantId("t"), "k", 1, 1, 2, 1, spki,
            new List<string> { "s1", "s2" }, new List<string[]> { new[] { "c" } }));

        Assert.Throws<ArgumentException>(() => new ValidatedGrantClaims(
            "i", "a", "g", "n", new TenantId("t"), "k", 1, 1, 2, 1, spki,
            new List<string> { "s1" }, new List<string[]> { null! }));

        Assert.Throws<ArgumentException>(() => new ValidatedGrantClaims(
            "i", "a", "g", "n", new TenantId("t"), "k", 1, 1, 2, 1, spki,
            new List<string> { "s1" }, new List<string[]> { new string[] { null! } }));
    }

    [Fact]
    public void P256_identity_helper_accepts_p256_and_refuses_other_curves()
    {
        using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using var p521 = ECDsa.Create(ECCurve.NamedCurves.nistP521);

        Assert.True(P256Curve.IsP256(p256));
        Assert.False(P256Curve.IsP256(p384));
        Assert.False(P256Curve.IsP256(p521));
        Assert.Equal("1.2.840.10045.3.1.7", P256Curve.OidValue);

        // Another 256-BIT curve (secp256k1) must not pass the identity check: key size alone is
        // not identity. Platform support varies; where it can be created it must be refused.
        try
        {
            using var k1 = ECDsa.Create(ECCurve.CreateFromValue("1.3.132.0.10"));
            Assert.Equal(256, k1.KeySize);
            Assert.False(P256Curve.IsP256(k1));
        }
        catch (CryptographicException)
        {
            // Platform does not provide secp256k1; the OID constant assertion still pins identity.
        }
        catch (PlatformNotSupportedException)
        {
        }
    }
}

namespace FaraTaraz.InstallationAuthority.Tests;

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Stage-A verification tests (W4-C1): strict JWS parsing and ES256 signature verification.
/// These prove what C1 does — signature and claim structure — and explicitly do NOT test
/// possession, admission-time expiry, job authorization or anything that would imply tenant
/// authority. Every key is ephemeral and test-only.
/// </summary>
public sealed class GrantVerifierTests
{
    private static readonly DateTimeOffset AnyTime = DateTimeOffset.UnixEpoch;

    private static TrustedAuthorityKeySet KeySet(ECDsa companyKey, string kid = TestGrantSigner.KeyId)
        => TrustedAuthorityKeySet.Create(new[]
        {
            new KeyValuePair<string, byte[]>(kid, companyKey.ExportSubjectPublicKeyInfo()),
        });

    private static GrantVerificationResult Verify(
        ECDsa companyKey, string payloadJson, string? kid = null, string? alg = null, string? typ = null,
        byte[]? signatureOverride = null, CancellationToken cancellationToken = default)
        => GrantVerifier.Verify(
            TestGrantSigner.Sign(companyKey, payloadJson, kid, alg ?? "ES256", typ ?? "FT-GRANT+JWS", signatureOverride),
            KeySet(companyKey),
            cancellationToken);

    [Fact]
    public void Valid_grant_yields_immutable_signature_validated_claims()
    {
        using var company = TestGrantSigner.NewCompanyKey();
        using var installation = TestGrantSigner.NewCompanyKey();

        var claims = Verify(company, TestGrantSigner.Payload(TestGrantSigner.InstallationPublicKey(installation))).Claims;

        Assert.Equal("farataraz", claims.Issuer);
        Assert.Equal("farataraz-worker", claims.Audience);
        Assert.Equal("grant-1", claims.GrantId);
        Assert.Equal("install-1", claims.InstallationId);
        Assert.Equal("tenant-1", claims.TenantId.Value);
        Assert.Equal(TestGrantSigner.KeyId, claims.SigningKeyId);
        Assert.Equal(1000, claims.IssuedAtUnixSeconds);
        Assert.Equal(1000, claims.NotBeforeUnixSeconds);
        Assert.Equal(2000, claims.ExpiresAtUnixSeconds);
        Assert.Equal(30, claims.MaxOfflineDays);
        Assert.Equal(1, claims.ScopeCount);
        Assert.Equal("src-1", claims.ScopeSourceId(0).Value);
        Assert.Equal(2, claims.CapabilityCount(0));
        Assert.Equal("Customers", claims.Capability(0, 0));
        Assert.Equal("Products", claims.Capability(0, 1));

        // Defensive copy: the returned array is a fresh instance, never the backing store.
        var a = claims.CopyInstallationPublicKeySpki();
        var b = claims.CopyInstallationPublicKeySpki();
        Assert.NotSame(a, b);
        a[0] ^= 0xFF;
        Assert.NotEqual(a[0], b[0]);
    }

    [Fact]
    public void Tampered_payload_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();
        using var installation = TestGrantSigner.NewCompanyKey();

        var valid = TestGrantSigner.Sign(company, TestGrantSigner.Payload(TestGrantSigner.InstallationPublicKey(installation)));
        var text = Encoding.ASCII.GetString(valid);
        // Flip a byte in the payload segment, leaving the signature untouched.
        var lastDot = text.LastIndexOf('.');
        var chars = text.ToCharArray();
        chars[lastDot - 1] = chars[lastDot - 1] == 'A' ? 'B' : 'A';

        var result = GrantVerifier.Verify(Encoding.ASCII.GetBytes(new string(chars)), KeySet(company), default);

        Assert.Equal(GrantVerificationFailure.InvalidSignature, result.Failure);
    }

    [Fact]
    public void Tampered_signature_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();
        using var installation = TestGrantSigner.NewCompanyKey();

        var signature = new byte[64];
        RandomNumberGenerator.Fill(signature);

        var result = Verify(
            company, TestGrantSigner.Payload(TestGrantSigner.InstallationPublicKey(installation)),
            signatureOverride: signature);

        Assert.Equal(GrantVerificationFailure.InvalidSignature, result.Failure);
    }

    [Fact]
    public void Unknown_key_id_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var result = Verify(company, TestGrantSigner.Payload("AA"), kid: "other-kid");

        Assert.Equal(GrantVerificationFailure.UnknownKey, result.Failure);
    }

    [Theory]
    [InlineData("RS256")]
    [InlineData("none")]
    [InlineData("ES384")]
    public void Wrong_algorithm_is_rejected(string alg)
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var result = Verify(company, TestGrantSigner.Payload("AA"), alg: alg);

        Assert.Equal(GrantVerificationFailure.InvalidAlgorithm, result.Failure);
    }

    [Fact]
    public void Wrong_type_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var result = Verify(company, TestGrantSigner.Payload("AA"), typ: "JWT");

        Assert.Equal(GrantVerificationFailure.InvalidAlgorithm, result.Failure);
    }

    [Fact]
    public void Der_or_wrong_length_signature_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var der = company.SignData(
            Encoding.ASCII.GetBytes("x"), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        var result = Verify(company, TestGrantSigner.Payload("AA"), signatureOverride: der);

        Assert.Equal(GrantVerificationFailure.Malformed, result.Failure);
    }

    [Fact]
    public void Grant_supplied_key_or_url_in_header_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var header = "{\"alg\":\"ES256\",\"kid\":\"test-company-key-1\",\"typ\":\"FT-GRANT+JWS\",\"jku\":\"https://evil\"}";
        var payload = TestGrantSigner.Payload("AA");
        var headerSegment = TestGrantSigner.Base64Url(Encoding.UTF8.GetBytes(header));
        var payloadSegment = TestGrantSigner.Base64Url(Encoding.UTF8.GetBytes(payload));
        var signingInput = Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment);
        var signature = company.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var grant = Encoding.ASCII.GetBytes(
            headerSegment + "." + payloadSegment + "." + TestGrantSigner.Base64Url(signature));

        var result = GrantVerifier.Verify(grant, KeySet(company), default);

        Assert.Equal(GrantVerificationFailure.InvalidAlgorithm, result.Failure);
    }

    [Fact]
    public void Inconsistent_signing_key_id_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var result = Verify(
            company, TestGrantSigner.Payload("AA", signingKeyId: "different-kid"));

        Assert.Equal(GrantVerificationFailure.InvalidClaims, result.Failure);
    }

    [Fact]
    public void Duplicate_payload_property_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();
        using var installation = TestGrantSigner.NewCompanyKey();

        var payload = TestGrantSigner.Payload(TestGrantSigner.InstallationPublicKey(installation))
            .Replace("\"grantId\":\"grant-1\"", "\"grantId\":\"grant-1\",\"grantId\":\"grant-2\"");

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(company, payload).Failure);
    }

    [Fact]
    public void Duplicate_property_inside_a_scope_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();
        using var installation = TestGrantSigner.NewCompanyKey();

        var scopes = "[{\"sourceId\":\"src-1\",\"sourceId\":\"src-2\",\"capabilities\":[\"Customers\"]}]";
        var payload = TestGrantSigner.Payload(TestGrantSigner.InstallationPublicKey(installation), scopes: scopes);

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(company, payload).Failure);
    }

    [Fact]
    public void Missing_or_wrong_type_claims_are_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var missing = TestGrantSigner.Payload("AA").Replace(",\"signingKeyId\":\"test-company-key-1\"", string.Empty);
        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(company, missing).Failure);

        var wrongType = TestGrantSigner.Payload("AA").Replace("\"tenantId\":\"tenant-1\"", "\"tenantId\":42");
        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(company, wrongType).Failure);
    }

    [Theory]
    [InlineData("2000", "1000", "3000")]   // nbf < iat
    [InlineData("1000", "1000", "1000")]   // exp <= nbf
    [InlineData("1000", "1000", "999999999")] // lifetime exceeds 30 days
    [InlineData("-5", "1000", "2000")]     // negative iat
    public void Invalid_timestamp_order_or_range_is_rejected(string iat, string nbf, string exp)
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var payload = TestGrantSigner.Payload("AA", iat: iat, nbf: nbf, exp: exp);

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(company, payload).Failure);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("31")]
    public void Invalid_offline_bound_is_rejected(string maxOfflineDays)
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var payload = TestGrantSigner.Payload("AA", maxOfflineDays: maxOfflineDays);

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(company, payload).Failure);
    }

    [Fact]
    public void Invalid_installation_public_key_encoding_is_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(company, TestGrantSigner.Payload("not-a-key")).Failure);

        // A valid P-256 key of the wrong curve (P-384) must also be refused.
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var spki = TestGrantSigner.Base64Url(p384.ExportSubjectPublicKeyInfo());
        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(company, TestGrantSigner.Payload(spki)).Failure);
    }

    [Fact]
    public void Malformed_encoding_and_json_are_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        Assert.Equal(GrantVerificationFailure.Malformed,
            GrantVerifier.Verify(Encoding.ASCII.GetBytes("not-a-jws"), KeySet(company), default).Failure);

        // Only two segments.
        Assert.Equal(GrantVerificationFailure.Malformed,
            GrantVerifier.Verify(Encoding.ASCII.GetBytes("aaa.bbb"), KeySet(company), default).Failure);

        // Four segments.
        Assert.Equal(GrantVerificationFailure.Malformed,
            GrantVerifier.Verify(Encoding.ASCII.GetBytes("aaa.bbb.ccc.ddd"), KeySet(company), default).Failure);

        // Non-base64url byte.
        Assert.Equal(GrantVerificationFailure.Malformed,
            GrantVerifier.Verify(Encoding.ASCII.GetBytes("aa*a.bbb.ccc"), KeySet(company), default).Failure);

        // Signed but malformed payload JSON.
        Assert.Equal(GrantVerificationFailure.Malformed, Verify(company, "{ not json ").Failure);
    }

    [Fact]
    public void Oversize_input_and_limits_are_rejected()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        Assert.Equal(GrantVerificationFailure.Malformed,
            GrantVerifier.Verify(new byte[JwsCompact.MaxGrantBytes + 1], KeySet(company), default).Failure);

        // Excessive JSON depth.
        var deep = new string('[', 12) + new string(']', 12);
        Assert.Equal(GrantVerificationFailure.Malformed, Verify(company, deep).Failure);

        // Too many scopes.
        var scopes = string.Join(",", Enumerable.Range(0, 65)
            .Select(i => $"{{\"sourceId\":\"s{i}\",\"capabilities\":[\"Customers\"]}}"));
        Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(company, TestGrantSigner.Payload("AA", scopes: "[" + scopes + "]")).Failure);

        // Too many capabilities in one scope.
        var capabilities = string.Join(",", Enumerable.Range(0, 9).Select(i => $"\"c{i}\""));
        Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(company, TestGrantSigner.Payload("AA", scopes: $"[{{\"sourceId\":\"s\",\"capabilities\":[{capabilities}]}}]")).Failure);
    }

    [Fact]
    public void Unprovisioned_key_store_fails_closed()
    {
        using var company = TestGrantSigner.NewCompanyKey();

        var grant = TestGrantSigner.Sign(company, TestGrantSigner.Payload("AA"));

        Assert.False(TrustedAuthorityKeySet.Unprovisioned.IsProvisioned);
        Assert.Equal(GrantVerificationFailure.UnknownKey,
            GrantVerifier.Verify(grant, TrustedAuthorityKeySet.Unprovisioned, default).Failure);
    }

    [Fact]
    public void Already_cancelled_token_propagates()
    {
        using var company = TestGrantSigner.NewCompanyKey();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => Verify(company, TestGrantSigner.Payload("AA"), cancellationToken: cts.Token));
    }

    [Fact]
    public void Verification_core_has_no_minting_reference_or_production_friend()
    {
        var assembly = typeof(GrantVerifier).Assembly;
        var references = assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        // Inward only: shared identifiers from the foundation. No Application/Infrastructure/EF/Npgsql.
        Assert.Contains("FaraTaraz.BuildingBlocks", references);
        Assert.DoesNotContain(references, n => n.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, n => n.Contains("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, n => n.Contains("MediatR", StringComparison.Ordinal));

        // The verification core never touches tenant authority.
        var types = assembly.GetTypes().Select(t => t.Name).ToList();
        Assert.DoesNotContain(types, n => n.Contains("TenantContext", StringComparison.Ordinal));
        Assert.DoesNotContain(types, n => n.Contains("DatabaseTenantScope", StringComparison.Ordinal));
    }
}

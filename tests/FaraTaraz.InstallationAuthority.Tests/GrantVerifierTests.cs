namespace FaraTaraz.InstallationAuthority.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Stage-A verification tests (W4-C1 + C1-R1): strict JWS parsing and ES256 signature verification.
///
/// Every negative case starts from an OTHERWISE-VALID, correctly signed grant and mutates exactly
/// one condition, so a rejection is attributable to that condition alone. These tests prove what
/// stage A does — signature and claim structure — and deliberately do not test possession,
/// admission-time expiry or job authorization.
/// </summary>
public sealed class GrantVerifierTests : IDisposable
{
    private readonly ECDsa _company = TestGrantSigner.NewCompanyKey();
    private readonly ECDsa _installation = TestGrantSigner.NewCompanyKey();
    private readonly string _installationSpki;
    private readonly TrustedAuthorityKeySet _keys;

    public GrantVerifierTests()
    {
        _installationSpki = TestGrantSigner.InstallationPublicKey(_installation);
        _keys = TrustedAuthorityKeySet.Create(new[]
        {
            new KeyValuePair<string, byte[]>(TestGrantSigner.KeyId, _company.ExportSubjectPublicKeyInfo()),
        });
    }

    public void Dispose()
    {
        _company.Dispose();
        _installation.Dispose();
    }

    private string ValidPayload(
        string? signingKeyId = null, string? scopes = null, string? iat = null, string? nbf = null,
        string? exp = null, string? maxOfflineDays = null, string? installationPublicKey = null,
        string? tenantId = null)
        => TestGrantSigner.Payload(
            installationPublicKey ?? _installationSpki, signingKeyId, scopes, iat, nbf, exp, maxOfflineDays,
            tenantId ?? "\"tenant-1\"");

    private GrantVerificationResult Verify(
        string payload, string? kid = null, string? alg = null, string? typ = null,
        byte[]? signatureOverride = null, TrustedAuthorityKeySet? keys = null, CancellationToken cancellationToken = default)
        => GrantVerifier.Verify(
            TestGrantSigner.Sign(_company, payload, kid, alg ?? "ES256", typ ?? "FT-GRANT+JWS", signatureOverride),
            keys ?? _keys,
            cancellationToken);

    private GrantVerificationResult VerifyRaw(byte[] grant, TrustedAuthorityKeySet? keys = null)
        => GrantVerifier.Verify(grant, keys ?? _keys, default);

    // --- Positive: valid grant and immutable claims ----------------------------------------

    [Fact]
    public void Valid_grant_yields_immutable_signature_validated_claims()
    {
        var claims = Verify(ValidPayload()).Claims;

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

        Assert.Throws<ArgumentOutOfRangeException>(() => claims.ScopeSourceId(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => claims.Capability(0, 2));

        var a = claims.CopyInstallationPublicKeySpki();
        var b = claims.CopyInstallationPublicKeySpki();
        Assert.NotSame(a, b);
        a[0] ^= 0xFF;
        Assert.NotEqual(a[0], b[0]);
    }

    // --- Signature ------------------------------------------------------------------------

    [Fact]
    public void Tampered_payload_is_rejected()
    {
        var grant = TestGrantSigner.Sign(_company, ValidPayload());
        var text = Encoding.ASCII.GetString(grant);
        var chars = text.ToCharArray();
        chars[chars.Length / 2] = chars[chars.Length / 2] == 'A' ? 'B' : 'A';

        Assert.Equal(GrantVerificationFailure.InvalidSignature, VerifyRaw(Encoding.ASCII.GetBytes(new string(chars))).Failure);
    }

    [Fact]
    public void Tampered_signature_is_rejected()
    {
        var signature = new byte[64];
        RandomNumberGenerator.Fill(signature);

        Assert.Equal(GrantVerificationFailure.InvalidSignature,
            Verify(ValidPayload(), signatureOverride: signature).Failure);
    }

    [Fact]
    public void Der_or_wrong_length_signature_is_rejected()
    {
        var der = _company.SignData(
            Encoding.ASCII.GetBytes("x"), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        Assert.Equal(GrantVerificationFailure.Malformed,
            Verify(ValidPayload(), signatureOverride: der).Failure);
    }

    // --- Header ---------------------------------------------------------------------------

    [Fact]
    public void Unknown_key_id_is_rejected()
        => Assert.Equal(GrantVerificationFailure.UnknownKey, Verify(ValidPayload(), kid: "other-kid").Failure);

    [Theory]
    [InlineData("RS256")]
    [InlineData("none")]
    [InlineData("ES384")]
    public void Wrong_algorithm_is_rejected(string alg)
        => Assert.Equal(GrantVerificationFailure.InvalidAlgorithm, Verify(ValidPayload(), alg: alg).Failure);

    [Fact]
    public void Wrong_type_is_rejected()
        => Assert.Equal(GrantVerificationFailure.InvalidAlgorithm, Verify(ValidPayload(), typ: "JWT").Failure);

    [Fact]
    public void Grant_supplied_key_or_url_in_header_is_rejected()
    {
        var header = "{\"alg\":\"ES256\",\"kid\":\"" + TestGrantSigner.KeyId + "\",\"typ\":\"FT-GRANT+JWS\",\"jku\":\"https://evil\"}";

        Assert.Equal(GrantVerificationFailure.InvalidAlgorithm,
            VerifyRaw(SignWithHeader(header, ValidPayload())).Failure);
    }

    // --- Claims: types and values ----------------------------------------------------------

    [Fact]
    public void Inconsistent_signing_key_id_is_rejected()
        => Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(ValidPayload(signingKeyId: "different-kid")).Failure);

    [Fact]
    public void Duplicate_payload_property_is_rejected()
    {
        var payload = ValidPayload().Replace("\"grantId\":\"grant-1\"", "\"grantId\":\"grant-1\",\"grantId\":\"grant-2\"");

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(payload).Failure);
    }

    [Fact]
    public void Duplicate_property_inside_a_scope_is_rejected()
    {
        var scopes = "[{\"sourceId\":\"src-1\",\"sourceId\":\"src-2\",\"capabilities\":[\"Customers\"]}]";

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(ValidPayload(scopes: scopes)).Failure);
    }

    [Fact]
    public void Missing_claim_is_rejected()
    {
        var missing = ValidPayload().Replace(",\"signingKeyId\":\"" + TestGrantSigner.KeyId + "\"", string.Empty);

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(missing).Failure);
    }

    [Fact]
    public void Wrong_type_string_claim_is_rejected()
        => Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(ValidPayload(tenantId: "42")).Failure);

    [Theory]
    [InlineData("\"1000\"")]   // string
    [InlineData("null")]       // null
    [InlineData("true")]       // bool
    [InlineData("{}")]         // object
    [InlineData("[]")]         // array
    public void Wrong_type_numeric_claim_fails_verification_instead_of_throwing(string value)
    {
        // The grant is otherwise valid and correctly signed: only the numeric type is wrong.
        var payload = TestGrantSigner.Payload(_installationSpki).Replace("\"iat\":1000", "\"iat\":" + value);

        var result = Verify(payload);

        Assert.Equal(GrantVerificationFailure.InvalidClaims, result.Failure);
        Assert.Throws<InvalidOperationException>(() => result.Claims);
    }

    [Theory]
    [InlineData("\"30\"")]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("{}")]
    [InlineData("[30]")]
    public void Wrong_type_offline_bound_fails_verification_instead_of_throwing(string value)
    {
        var payload = TestGrantSigner.Payload(_installationSpki).Replace("\"maxOfflineDays\":30", "\"maxOfflineDays\":" + value);

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(payload).Failure);
    }

    [Theory]
    [InlineData("2000", "1000", "3000")]        // nbf < iat
    [InlineData("1000", "1000", "1000")]        // exp <= nbf
    [InlineData("1000", "1000", "999999999")]   // lifetime exceeds 30 days
    [InlineData("-5", "1000", "2000")]          // negative iat
    public void Invalid_timestamp_order_or_range_is_rejected(string iat, string nbf, string exp)
        => Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(ValidPayload(iat: iat, nbf: nbf, exp: exp)).Failure);

    [Theory]
    [InlineData("-1")]
    [InlineData("31")]
    public void Invalid_offline_bound_is_rejected(string maxOfflineDays)
        => Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(ValidPayload(maxOfflineDays: maxOfflineDays)).Failure);

    // --- Claims: installation key identity and encoding ------------------------------------

    [Fact]
    public void Invalid_installation_public_key_is_rejected()
        => Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(ValidPayload(installationPublicKey: "not-a-key")).Failure);

    [Fact]
    public void Another_curve_installation_key_is_rejected_by_curve_identity()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var p384Spki = TestGrantSigner.Base64Url(p384.ExportSubjectPublicKeyInfo());

        Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(ValidPayload(installationPublicKey: p384Spki)).Failure);
    }

    [Theory]
    [InlineData("=")]
    [InlineData("+")]
    [InlineData("/")]
    [InlineData(" ")]
    public void Non_base64url_installation_key_encoding_is_rejected(string injected)
    {
        // Inject an otherwise-decoded key string with a forbidden character; the grant is signed,
        // so only the nested encoding is wrong.
        var tampered = _installationSpki[..(_installationSpki.Length - 1)] + injected;

        Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(ValidPayload(installationPublicKey: tampered)).Failure);
    }

    // --- Claims: bounds and limits ---------------------------------------------------------

    [Fact]
    public void Too_many_scopes_is_rejected()
    {
        var scopes = "[" + string.Join(",", Enumerable.Range(0, 65)
            .Select(i => $"{{\"sourceId\":\"s{i}\",\"capabilities\":[\"Customers\"]}}")) + "]";

        // A VALID installation key, so the count guard is what rejects this payload.
        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(ValidPayload(scopes: scopes)).Failure);
    }

    [Fact]
    public void Too_many_capabilities_is_rejected()
    {
        var capabilities = string.Join(",", Enumerable.Range(0, 9).Select(i => $"\"c{i}\""));
        var scopes = $"[{{\"sourceId\":\"s\",\"capabilities\":[{capabilities}]}}]";

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(ValidPayload(scopes: scopes)).Failure);
    }

    [Fact]
    public void Empty_scopes_or_capabilities_are_rejected()
    {
        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(ValidPayload(scopes: "[]")).Failure);
        Assert.Equal(GrantVerificationFailure.InvalidClaims,
            Verify(ValidPayload(scopes: "[{\"sourceId\":\"s\",\"capabilities\":[]}]")).Failure);
    }

    [Fact]
    public void Duplicate_scope_source_is_rejected()
    {
        var scopes = "[{\"sourceId\":\"s\",\"capabilities\":[\"Customers\"]},{\"sourceId\":\"s\",\"capabilities\":[\"Products\"]}]";

        Assert.Equal(GrantVerificationFailure.InvalidClaims, Verify(ValidPayload(scopes: scopes)).Failure);
    }

    // --- Encoding, structure and size ------------------------------------------------------

    [Fact]
    public void Malformed_compact_structure_is_rejected()
    {
        Assert.Equal(GrantVerificationFailure.Malformed, VerifyRaw(Encoding.ASCII.GetBytes("not-a-jws")).Failure);
        Assert.Equal(GrantVerificationFailure.Malformed, VerifyRaw(Encoding.ASCII.GetBytes("aaa.bbb")).Failure);
        Assert.Equal(GrantVerificationFailure.Malformed, VerifyRaw(Encoding.ASCII.GetBytes("aaa.bbb.ccc.ddd")).Failure);
        Assert.Equal(GrantVerificationFailure.Malformed, VerifyRaw(Encoding.ASCII.GetBytes("aa*a.bbb.ccc")).Failure);
        Assert.Equal(GrantVerificationFailure.Malformed, VerifyRaw(Encoding.ASCII.GetBytes("aa=.bbb.ccc")).Failure);
    }

    [Fact]
    public void Noncanonical_or_non_base64url_outer_segment_is_rejected()
    {
        var valid = Encoding.ASCII.GetString(TestGrantSigner.Sign(_company, ValidPayload()));
        var prefix = valid[..valid.LastIndexOf('.')];

        // Signature segment length % 4 == 2 with non-zero unused pad bits.
        var noncanonical = new string('A', 85) + "B";
        Assert.Equal(2, noncanonical.Length % 4);
        Assert.Equal(GrantVerificationFailure.Malformed, VerifyRaw(Encoding.ASCII.GetBytes(prefix + "." + noncanonical)).Failure);

        // Padding and +/ inside the outer signature segment.
        Assert.Equal(GrantVerificationFailure.Malformed, VerifyRaw(Encoding.ASCII.GetBytes(prefix + ".AA==")).Failure);
        Assert.Equal(GrantVerificationFailure.Malformed, VerifyRaw(Encoding.ASCII.GetBytes(prefix + ".AA+A")).Failure);
        Assert.Equal(GrantVerificationFailure.Malformed, VerifyRaw(Encoding.ASCII.GetBytes(prefix + ".AA A")).Failure);
    }

    [Fact]
    public void Strict_base64url_decoder_rejects_noncanonical_encodings()
    {
        Assert.True(JwsCompact.TryDecodeBase64Url("AA", out var two));
        Assert.Single(two);

        Assert.False(JwsCompact.TryDecodeBase64Url("A", out _));        // impossible length % 4 == 1
        Assert.False(JwsCompact.TryDecodeBase64Url("AA==", out _));     // padding
        Assert.False(JwsCompact.TryDecodeBase64Url("AA+A", out _));     // '+'
        Assert.False(JwsCompact.TryDecodeBase64Url("AA/A", out _));     // '/'
        Assert.False(JwsCompact.TryDecodeBase64Url("AA A", out _));     // whitespace
        Assert.False(JwsCompact.TryDecodeBase64Url("AAB", out _));      // non-zero unused pad bits
        Assert.False(JwsCompact.TryDecodeBase64Url(string.Empty, out _));
    }

    [Fact]
    public void Malformed_payload_json_is_rejected()
        => Assert.Equal(GrantVerificationFailure.Malformed, Verify("{ not json ").Failure);

    [Fact]
    public void Oversize_input_and_excessive_depth_are_rejected()
    {
        Assert.Equal(GrantVerificationFailure.Malformed,
            VerifyRaw(new byte[JwsCompact.MaxGrantBytes + 1]).Failure);

        var deep = new string('[', 12) + new string(']', 12);
        Assert.Equal(GrantVerificationFailure.Malformed, Verify(deep).Failure);
    }

    // --- Trust store and cancellation ------------------------------------------------------

    [Fact]
    public void Unprovisioned_key_store_fails_closed()
    {
        Assert.False(TrustedAuthorityKeySet.Unprovisioned.IsProvisioned);
        Assert.Equal(GrantVerificationFailure.UnknownKey,
            Verify(ValidPayload(), keys: TrustedAuthorityKeySet.Unprovisioned).Failure);
    }

    [Fact]
    public void Non_p256_trust_store_key_is_refused()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        Assert.Throws<ArgumentException>(() => TrustedAuthorityKeySet.Create(new[]
        {
            new KeyValuePair<string, byte[]>("k", p384.ExportSubjectPublicKeyInfo()),
        }));
    }

    [Fact]
    public void Already_cancelled_token_propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => Verify(ValidPayload(), cancellationToken: cts.Token));
    }

    // --- Assembly surface ------------------------------------------------------------------

    [Fact]
    public void Authority_core_exposes_no_public_surface_and_only_its_test_assembly_as_friend()
    {
        var assembly = typeof(GrantVerifier).Assembly;

        // Exact friend list: the test assembly only.
        var friends = assembly
            .GetCustomAttributes(typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute), false)
            .Cast<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName.Split(',')[0])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(new[] { "FaraTaraz.InstallationAuthority.Tests" }, friends);

        // Exact inward reference set for FaraTaraz assemblies.
        var faraTarazReferences = assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("FaraTaraz", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(new[] { "FaraTaraz.BuildingBlocks" }, faraTarazReferences);

        // No public type in the verification namespace: nothing here can be misused as an
        // "already verified" assertion API. (Note: this is a surface assertion; it does not by
        // itself prove that no code anywhere uses external tenant authority.)
        var publicTypes = assembly.GetTypes()
            .Where(t => t.Namespace == "FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants"
                        && (t.IsPublic || t.IsNestedPublic))
            .Select(t => t.FullName)
            .ToList();
        Assert.Empty(publicTypes);
    }

    private byte[] SignWithHeader(string headerJson, string payloadJson)
    {
        var headerSegment = TestGrantSigner.Base64Url(Encoding.UTF8.GetBytes(headerJson));
        var payloadSegment = TestGrantSigner.Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment);
        var signature = _company.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment + "." + TestGrantSigner.Base64Url(signature));
    }
}

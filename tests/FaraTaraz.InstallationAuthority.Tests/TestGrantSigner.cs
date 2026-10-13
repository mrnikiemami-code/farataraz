namespace FaraTaraz.InstallationAuthority.Tests;

using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Test-only ephemeral ES256 grant signer. It creates an in-memory P-256 key and produces compact
/// JWS grants for the verification tests. No private key is written to source, fixtures, logs or
/// configuration — the key exists only for the lifetime of a test. This seam produces grants only;
/// it never creates a TenantContext.
/// </summary>
internal static class TestGrantSigner
{
    internal const string KeyId = "test-company-key-1";

    internal static ECDsa NewCompanyKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    internal static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Default valid payload; individual fields can be replaced by the caller.</summary>
    internal static string Payload(
        string installationPublicKeyBase64Url,
        string? signingKeyId = null,
        string? scopes = null,
        string? iat = null,
        string? nbf = null,
        string? exp = null,
        string? maxOfflineDays = null,
        string? tenantId = "\"tenant-1\"",
        string? installationId = "\"install-1\"")
    {
        scopes ??= "[{\"sourceId\":\"src-1\",\"capabilities\":[\"Customers\",\"Products\"]}]";

        return "{" +
            "\"iss\":\"farataraz\",\"aud\":\"farataraz-worker\"," +
            "\"grantId\":\"grant-1\"," +
            $"\"installationId\":{installationId}," +
            $"\"installationPublicKey\":\"{installationPublicKeyBase64Url}\"," +
            $"\"tenantId\":{tenantId}," +
            $"\"scopes\":{scopes}," +
            $"\"iat\":{iat ?? "1000"}," +
            $"\"nbf\":{nbf ?? "1000"}," +
            $"\"exp\":{exp ?? "2000"}," +
            $"\"maxOfflineDays\":{maxOfflineDays ?? "30"}," +
            $"\"signingKeyId\":\"{signingKeyId ?? KeyId}\"" +
            "}";
    }

    /// <summary>Assembles and signs a compact JWS over the exact header.payload bytes.</summary>
    internal static byte[] Sign(
        ECDsa companyKey,
        string payloadJson,
        string? kid = null,
        string? alg = "ES256",
        string? typ = "FT-GRANT+JWS",
        byte[]? signatureOverride = null)
    {
        var headerJson = "{" +
            $"\"alg\":\"{alg}\",\"kid\":\"{kid ?? KeyId}\",\"typ\":\"{typ}\"" +
            "}";

        var headerSegment = Base64Url(Encoding.UTF8.GetBytes(headerJson));
        var payloadSegment = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment);

        var signature = signatureOverride ?? companyKey.SignData(
            signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment + "." + Base64Url(signature));
    }

    internal static string InstallationPublicKey(ECDsa installationKey)
        => Base64Url(installationKey.ExportSubjectPublicKeyInfo());
}

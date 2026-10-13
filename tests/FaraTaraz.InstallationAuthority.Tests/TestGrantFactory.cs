namespace FaraTaraz.InstallationAuthority.Tests;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Builds otherwise-valid, correctly signed C1 claims (and the matching installation private key)
/// so stage-B negative cases can mutate exactly one condition at a time.
///
/// TEST-ONLY. Keys are ephemeral; nothing here is production configuration and no key material is
/// written to source, fixtures, logs or committed files.
/// </summary>
internal static class TestGrantFactory
{
    internal const string Issuer = "farataraz";
    internal const string Audience = "farataraz-worker";

    internal static (ValidatedGrantClaims Claims, ECDsa Installation) Create(
        string installationId = "install-1",
        string tenantId = "tenant-1",
        string sourceId = "src-1",
        string capabilitiesJson = "\"Customers\",\"Products\"",
        long iat = 1000,
        long nbf = 1000,
        long exp = 2000,
        int maxOfflineDays = 30,
        string issuer = Issuer,
        string audience = Audience,
        ECDsa? installation = null)
    {
        var installationKey = installation ?? TestGrantSigner.NewCompanyKey();
        var spki = TestGrantSigner.InstallationPublicKey(installationKey);
        var scopes = $"[{{\"sourceId\":\"{sourceId}\",\"capabilities\":[{capabilitiesJson}]}}]";

        var payload = TestGrantSigner
            .Payload(
                spki,
                scopes: scopes,
                iat: iat.ToString(),
                nbf: nbf.ToString(),
                exp: exp.ToString(),
                maxOfflineDays: maxOfflineDays.ToString(),
                tenantId: "\"" + tenantId + "\"",
                installationId: "\"" + installationId + "\"")
            .Replace("\"iss\":\"" + Issuer + "\"", "\"iss\":\"" + issuer + "\"", StringComparison.Ordinal)
            .Replace("\"aud\":\"" + Audience + "\"", "\"aud\":\"" + audience + "\"", StringComparison.Ordinal);

        using var company = TestGrantSigner.NewCompanyKey();
        var keys = TrustedAuthorityKeySet.Create(new[]
        {
            new KeyValuePair<string, byte[]>(TestGrantSigner.KeyId, company.ExportSubjectPublicKeyInfo()),
        });

        var result = GrantVerifier.Verify(TestGrantSigner.Sign(company, payload), keys, default);

        if (!result.IsValid)
        {
            throw new InvalidOperationException($"Test fixture did not produce valid claims ({result.Failure}).");
        }

        return (result.Claims, installationKey);
    }

    internal static GrantAuthorityExpectations Expectations() => new(Issuer, Audience);

    internal static byte[] Sign(ECDsa installationKey, byte[] message)
        => installationKey.SignData(
            message, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
}

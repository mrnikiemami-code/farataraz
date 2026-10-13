namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Stage-A installation-grant verification (W4-C1): ES256 signature verification over a strictly
/// parsed compact JWS, producing immutable <see cref="ValidatedGrantClaims"/>.
///
/// <b>This is not authorization.</b> A valid result means only "a company-pinned key signed these
/// well-formed claims". Possession proof, admission-time expiry, requested-job membership, offline
/// policy and tenant/job authority are stage B and are deliberately absent here.
///
/// The signature is verified over the ORIGINAL encoded <c>header.payload</c> bytes as received —
/// JSON is never re-serialised before verification. Signature encoding is ES256 P1363 (r||s), not
/// DER. Expected malformed/invalid input is returned as a failure result, never thrown;
/// cancellation propagates.
/// </summary>
internal static class GrantVerifier
{
    private const int SignatureBytes = 64; // ES256 P1363 r||s

    /// <summary>
    /// Verifies a compact JWS grant against the pinned company trust store and returns either
    /// immutable signature-validated claims or a failure reason.
    /// </summary>
    internal static GrantVerificationResult Verify(
        ReadOnlySpan<byte> grant, TrustedAuthorityKeySet keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        cancellationToken.ThrowIfCancellationRequested();

        if (grant.Length == 0 || grant.Length > JwsCompact.MaxGrantBytes || !JwsCompact.IsCompactAscii(grant))
        {
            return GrantVerificationResult.Rejected(GrantVerificationFailure.Malformed);
        }

        if (!JwsCompact.TrySplitSegments(
                Encoding.ASCII.GetString(grant), out var headerSegment, out var payloadSegment, out var signatureSegment))
        {
            return GrantVerificationResult.Rejected(GrantVerificationFailure.Malformed);
        }

        if (!JwsCompact.TryDecodeBase64Url(headerSegment, out var headerJson) ||
            !JwsCompact.TryDecodeBase64Url(payloadSegment, out var payloadJson) ||
            !JwsCompact.TryDecodeBase64Url(signatureSegment, out var signature) ||
            signature.Length != SignatureBytes)
        {
            return GrantVerificationResult.Rejected(GrantVerificationFailure.Malformed);
        }

        var headerFailure = GrantClaimsReader.TryReadHeader(headerJson, out var keyId);
        if (headerFailure != GrantVerificationFailure.None)
        {
            return GrantVerificationResult.Rejected(headerFailure);
        }

        if (!keys.TryResolve(keyId, out var companyKey) || companyKey is null)
        {
            return GrantVerificationResult.Rejected(GrantVerificationFailure.UnknownKey);
        }

        using (companyKey)
        {
            var verified = companyKey.VerifyData(
                JwsCompact.SigningInput(headerSegment, payloadSegment),
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            if (!verified)
            {
                return GrantVerificationResult.Rejected(GrantVerificationFailure.InvalidSignature);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var claimsFailure = GrantClaimsReader.TryReadClaims(payloadJson, keyId, out var claims);

        return claimsFailure == GrantVerificationFailure.None
            ? GrantVerificationResult.Valid(claims)
            : GrantVerificationResult.Rejected(claimsFailure);
    }
}

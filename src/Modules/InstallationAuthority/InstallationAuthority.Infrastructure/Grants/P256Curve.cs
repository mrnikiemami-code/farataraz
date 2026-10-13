namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using System.Security.Cryptography;

/// <summary>
/// Named-curve identity for ES256. ES256 is defined over NIST P-256 only, identified by the
/// algorithm OID <c>1.2.840.10045.3.1.7</c>. A key size of 256 bits alone is NOT sufficient:
/// other 256-bit curves (for example secp256k1) are also 256 bits and are not ES256, so they must
/// be rejected by curve identity, not by size.
/// </summary>
internal static class P256Curve
{
    /// <summary>NIST P-256 / secp256r1 / prime256v1.</summary>
    internal const string OidValue = "1.2.840.10045.3.1.7";

    internal const string FriendlyName = "nistP256";

    /// <summary>
    /// True only for a named NIST P-256 key. The OID must match; the friendly name is consulted
    /// only when the platform reports no OID value, and it must then be exactly <c>nistP256</c>
    /// (so P-384 and other curves are still refused).
    /// </summary>
    internal static bool IsP256(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var oid = key.ExportParameters(includePrivateParameters: false).Curve.Oid;

        return string.Equals(oid.Value, OidValue, StringComparison.Ordinal)
            || (oid.Value is null && string.Equals(oid.FriendlyName, FriendlyName, StringComparison.Ordinal));
    }
}

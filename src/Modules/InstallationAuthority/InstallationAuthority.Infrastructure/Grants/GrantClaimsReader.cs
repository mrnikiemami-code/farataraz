namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using System.Security.Cryptography;
using System.Text.Json;
using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Strict, bounded claim reader: exact property sets, duplicate-property rejection at every object
/// level, bounded depth and bounded collection sizes, required types and value ranges. It is
/// JSON-only; signature verification happens before it is called. It produces signature-independent
/// structural validation results — running it without a verified signature authorizes nothing.
/// </summary>
internal static class GrantClaimsReader
{
    private const int MaxJsonDepth = 8;
    private const int MaxScopes = 64;
    private const int MaxCapabilitiesPerScope = 8;
    private const int MaxGrantLifetimeSeconds = 30 * 24 * 60 * 60;
    private const int MaxOfflineDaysLimit = 30;
    private const string ExpectedAlgorithm = "ES256";
    private const string ExpectedType = "FT-GRANT+JWS";

    private static readonly string[] HeaderProperties = { "alg", "kid", "typ" };

    private static readonly string[] PayloadProperties =
    {
        "iss", "aud", "grantId", "installationId", "installationPublicKey", "tenantId",
        "scopes", "iat", "nbf", "exp", "maxOfflineDays", "signingKeyId",
    };

    private static readonly string[] ScopeProperties = { "sourceId", "capabilities" };

    /// <summary>
    /// Validates the header and returns the key id that must be used for verification. Unknown or
    /// extra properties (including grant-supplied keys/URLs such as <c>jwk</c>/<c>jku</c>) and any
    /// algorithm other than ES256 are rejected.
    /// </summary>
    internal static GrantVerificationFailure TryReadHeader(byte[] headerJson, out string keyId)
    {
        keyId = string.Empty;

        if (!TryParseStrictObject(headerJson, out var header))
        {
            return GrantVerificationFailure.Malformed;
        }

        using (header)
        {
            var root = header.RootElement;

            if (!HasExactlyProperties(root, HeaderProperties, HeaderProperties.Length))
            {
                return GrantVerificationFailure.InvalidAlgorithm;
            }

            if (!TryString(root, "alg", out var alg) || !string.Equals(alg, ExpectedAlgorithm, StringComparison.Ordinal) ||
                !TryString(root, "typ", out var typ) || !string.Equals(typ, ExpectedType, StringComparison.Ordinal) ||
                !TryString(root, "kid", out var kid) || kid.Length == 0)
            {
                return GrantVerificationFailure.InvalidAlgorithm;
            }

            keyId = kid;
            return GrantVerificationFailure.None;
        }
    }

    /// <summary>
    /// Validates the payload against the header key id and builds immutable claims. Malformed JSON
    /// or invalid encoding is reported as <c>Malformed</c>; well-formed JSON with missing, mistyped
    /// or out-of-bounds claims as <c>InvalidClaims</c>. Neither outcome is authorization.
    /// </summary>
    internal static GrantVerificationFailure TryReadClaims(
        byte[] payloadJson, string keyId, out ValidatedGrantClaims claims)
    {
        claims = null!;

        if (!TryParseStrictObject(payloadJson, out var payload))
        {
            return GrantVerificationFailure.Malformed;
        }

        using (payload)
        {
            var root = payload.RootElement;

            if (!HasExactlyProperties(root, PayloadProperties, PayloadProperties.Length) ||
                !TryNonEmptyString(root, "iss", out var issuer) ||
                !TryNonEmptyString(root, "aud", out var audience) ||
                !TryNonEmptyString(root, "grantId", out var grantId) ||
                !TryNonEmptyString(root, "installationId", out var installationId) ||
                !TryNonEmptyString(root, "tenantId", out var tenantIdText) ||
                !TryNonEmptyString(root, "signingKeyId", out var signingKeyId))
            {
                return GrantVerificationFailure.InvalidClaims;
            }

            // The payload's signing key must be the header key that actually verified.
            if (!string.Equals(signingKeyId, keyId, StringComparison.Ordinal))
            {
                return GrantVerificationFailure.InvalidClaims;
            }

            if (!TryReadTimestamps(root, out var iat, out var nbf, out var exp, out var maxOfflineDays) ||
                !TryReadInstallationPublicKey(root, out var installationPublicKey) ||
                !TryReadScopes(root, out var scopeSourceIds, out var scopeCapabilities))
            {
                return GrantVerificationFailure.InvalidClaims;
            }

            claims = new ValidatedGrantClaims(
                issuer, audience, grantId, installationId, new TenantId(tenantIdText), signingKeyId,
                iat, nbf, exp, maxOfflineDays, installationPublicKey, scopeSourceIds, scopeCapabilities);

            return GrantVerificationFailure.None;
        }
    }

    private static bool TryReadTimestamps(
        JsonElement root, out long iat, out long nbf, out long exp, out int maxOfflineDays)
    {
        iat = nbf = exp = 0;
        maxOfflineDays = -1;

        // ValueKind is checked explicitly: a string/null/bool/object/array timestamp must fail
        // verification, never throw. There is deliberately no catch-all here.
        if (!TryNumber(root, "iat", out iat) ||
            !TryNumber(root, "nbf", out nbf) ||
            !TryNumber(root, "exp", out exp) ||
            !root.TryGetProperty("maxOfflineDays", out var offlineElement) ||
            offlineElement.ValueKind != JsonValueKind.Number ||
            !offlineElement.TryGetInt32(out maxOfflineDays))
        {
            return false;
        }

        return iat >= 0
            && nbf >= iat
            && exp > nbf
            && exp - iat <= MaxGrantLifetimeSeconds
            && maxOfflineDays is >= 0 and <= MaxOfflineDaysLimit;
    }

    /// <summary>Reads an integral JSON number, requiring the Number value kind first.</summary>
    private static bool TryNumber(JsonElement root, string name, out long value)
    {
        value = 0;

        return root.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt64(out value);
    }

    private static bool TryReadInstallationPublicKey(JsonElement root, out byte[] spki)
    {
        spki = Array.Empty<byte>();

        if (!TryNonEmptyString(root, "installationPublicKey", out var encoded) ||
            !JwsCompact.TryDecodeBase64Url(encoded, out var decoded))
        {
            return false;
        }

        try
        {
            using var probe = ECDsa.Create();
            probe.ImportSubjectPublicKeyInfo(decoded, out var read);

            // ES256 identity: named P-256 only, and the whole SPKI must be consumed.
            if (read != decoded.Length || !P256Curve.IsP256(probe))
            {
                return false;
            }
        }
        catch (CryptographicException)
        {
            return false;
        }

        spki = decoded;
        return true;
    }

    private static bool TryReadScopes(
        JsonElement root, out List<string> scopeSourceIds, out List<string[]> scopeCapabilities)
    {
        scopeSourceIds = new List<string>();
        scopeCapabilities = new List<string[]>();

        if (!root.TryGetProperty("scopes", out var scopes) ||
            scopes.ValueKind != JsonValueKind.Array ||
            scopes.GetArrayLength() == 0 ||
            scopes.GetArrayLength() > MaxScopes)
        {
            return false;
        }

        foreach (var scope in scopes.EnumerateArray())
        {
            if (scope.ValueKind != JsonValueKind.Object ||
                !HasExactlyProperties(scope, ScopeProperties, ScopeProperties.Length) ||
                !TryNonEmptyString(scope, "sourceId", out var sourceId) ||
                scopeSourceIds.Contains(sourceId, StringComparer.Ordinal) ||
                !TryReadCapabilities(scope, out var capabilities))
            {
                return false;
            }

            scopeSourceIds.Add(sourceId);
            scopeCapabilities.Add(capabilities);
        }

        return true;
    }

    private static bool TryReadCapabilities(JsonElement scope, out string[] capabilities)
    {
        capabilities = Array.Empty<string>();

        if (!scope.TryGetProperty("capabilities", out var element) ||
            element.ValueKind != JsonValueKind.Array ||
            element.GetArrayLength() == 0 ||
            element.GetArrayLength() > MaxCapabilitiesPerScope)
        {
            return false;
        }

        var list = new List<string>(element.GetArrayLength());

        foreach (var capability in element.EnumerateArray())
        {
            if (capability.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(capability.GetString()))
            {
                return false;
            }

            list.Add(capability.GetString()!);
        }

        capabilities = list.ToArray();
        return true;
    }

    /// <summary>Strict UTF-8/JSON parse with bounded depth; trailing content and comments rejected.</summary>
    private static bool TryParseStrictObject(byte[] utf8, out JsonDocument document)
    {
        document = null!;

        try
        {
            document = JsonDocument.Parse(utf8, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaxJsonDepth,
            });
        }
        catch (JsonException)
        {
            return false;
        }

        return document.RootElement.ValueKind == JsonValueKind.Object;
    }

    /// <summary>Requires exactly the allowed property names, rejecting unknown AND duplicate keys.</summary>
    private static bool HasExactlyProperties(JsonElement element, string[] allowed, int expectedCount)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;

        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                return false;
            }

            count++;
        }

        return count == expectedCount;
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()!;
        return true;
    }

    private static bool TryNonEmptyString(JsonElement element, string name, out string value)
        => TryString(element, name, out value) && value.Length > 0;
}

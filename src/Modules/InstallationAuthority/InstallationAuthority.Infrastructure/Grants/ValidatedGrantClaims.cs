namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Immutable, signature-validated grant claims (stage A output).
///
/// This is NOT tenant authority and NOT job authorization: it is the decoded, signature-verified
/// content of a grant. Obtaining an instance authorizes nothing.
///
/// Immutability is structural, not declared: all fields are <c>private readonly</c>; the
/// constructor validates and <b>deep-copies</b> every collection into private arrays; no backing
/// array, mutable nested object or <c>ReadOnlyMemory</c> over a private array is ever exposed
/// (scopes and capabilities are reachable only through index accessors). It is a sealed class —
/// not a record — so there is no compiler-generated <c>with</c>, <c>init</c> or value equality.
/// It has an internal constructor only.
/// </summary>
internal sealed class ValidatedGrantClaims
{
    private readonly string[] _scopeSourceIds;
    private readonly string[][] _scopeCapabilities;
    private readonly byte[] _installationPublicKeySpki;

    internal ValidatedGrantClaims(
        string issuer,
        string audience,
        string grantId,
        string installationId,
        TenantId tenantId,
        string signingKeyId,
        long issuedAtUnixSeconds,
        long notBeforeUnixSeconds,
        long expiresAtUnixSeconds,
        int maxOfflineDays,
        byte[] installationPublicKeySpki,
        IList<string> scopeSourceIds,
        IList<string[]> scopeCapabilities)
    {
        Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        Audience = audience ?? throw new ArgumentNullException(nameof(audience));
        GrantId = grantId ?? throw new ArgumentNullException(nameof(grantId));
        InstallationId = installationId ?? throw new ArgumentNullException(nameof(installationId));
        TenantId = tenantId;
        SigningKeyId = signingKeyId ?? throw new ArgumentNullException(nameof(signingKeyId));
        IssuedAtUnixSeconds = issuedAtUnixSeconds;
        NotBeforeUnixSeconds = notBeforeUnixSeconds;
        ExpiresAtUnixSeconds = expiresAtUnixSeconds;
        MaxOfflineDays = maxOfflineDays;

        ArgumentNullException.ThrowIfNull(installationPublicKeySpki);
        ArgumentNullException.ThrowIfNull(scopeSourceIds);
        ArgumentNullException.ThrowIfNull(scopeCapabilities);

        if (scopeSourceIds.Count != scopeCapabilities.Count)
        {
            throw new ArgumentException("Scope source ids and capability lists must have equal length.");
        }

        // Deep copy: never retain a caller-owned array.
        _installationPublicKeySpki = (byte[])installationPublicKeySpki.Clone();
        _scopeSourceIds = new string[scopeSourceIds.Count];
        _scopeCapabilities = new string[scopeCapabilities.Count][];

        for (var i = 0; i < scopeSourceIds.Count; i++)
        {
            _scopeSourceIds[i] = scopeSourceIds[i]
                ?? throw new ArgumentException("A scope source id must not be null.");

            var capabilities = scopeCapabilities[i]
                ?? throw new ArgumentException("A scope capability list must not be null.");

            var copy = new string[capabilities.Length];
            for (var c = 0; c < capabilities.Length; c++)
            {
                copy[c] = capabilities[c]
                    ?? throw new ArgumentException("A scope capability must not be null.");
            }

            _scopeCapabilities[i] = copy;
        }
    }

    internal string Issuer { get; }

    internal string Audience { get; }

    internal string GrantId { get; }

    internal string InstallationId { get; }

    internal TenantId TenantId { get; }

    internal string SigningKeyId { get; }

    internal long IssuedAtUnixSeconds { get; }

    internal long NotBeforeUnixSeconds { get; }

    internal long ExpiresAtUnixSeconds { get; }

    internal int MaxOfflineDays { get; }

    internal int ScopeCount => _scopeSourceIds.Length;

    internal AccountingSourceId ScopeSourceId(int scopeIndex)
        => new(_scopeSourceIds[Check(scopeIndex, _scopeSourceIds.Length)]);

    internal int CapabilityCount(int scopeIndex)
        => _scopeCapabilities[Check(scopeIndex, _scopeCapabilities.Length)].Length;

    internal string Capability(int scopeIndex, int capabilityIndex)
    {
        var capabilities = _scopeCapabilities[Check(scopeIndex, _scopeCapabilities.Length)];
        return capabilities[Check(capabilityIndex, capabilities.Length)];
    }

    /// <summary>
    /// Returns a fresh copy of the installation's SubjectPublicKeyInfo (validated grant data used
    /// by stage B for proof of possession). It is never a company verification key, and the
    /// private backing array is never exposed.
    /// </summary>
    internal byte[] CopyInstallationPublicKeySpki() => (byte[])_installationPublicKeySpki.Clone();

    private static int Check(int index, int length)
        => index >= 0 && index < length
            ? index
            : throw new ArgumentOutOfRangeException(nameof(index));
}

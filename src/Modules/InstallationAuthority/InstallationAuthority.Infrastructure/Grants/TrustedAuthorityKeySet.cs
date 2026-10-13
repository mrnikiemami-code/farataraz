namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using System.Security.Cryptography;

/// <summary>
/// Immutable pinned trust store: company grant-signing <b>public</b> keys keyed by <c>kid</c>.
///
/// It stores a <b>private deep copy</b> of each SubjectPublicKeyInfo; the caller's arrays are never
/// retained and no backing array is exposed. Only P-256 keys are accepted. It holds public keys
/// only — no private key material is stored, and the grant itself can never supply a key.
///
/// <see cref="Unprovisioned"/> is the fail-closed production default: with no approved company key
/// provisioned, every verification resolves no key and is rejected as <c>UnknownKey</c>.
/// </summary>
internal sealed class TrustedAuthorityKeySet
{
    private readonly Dictionary<string, byte[]> _spkiByKeyId;

    private TrustedAuthorityKeySet(Dictionary<string, byte[]> spkiByKeyId)
        => _spkiByKeyId = spkiByKeyId;

    /// <summary>Fail-closed default: no approved company key exists, so nothing can be verified.</summary>
    internal static TrustedAuthorityKeySet Unprovisioned { get; } =
        new(new Dictionary<string, byte[]>(StringComparer.Ordinal));

    /// <summary>True only when at least one approved company public key was provisioned.</summary>
    internal bool IsProvisioned => _spkiByKeyId.Count > 0;

    /// <summary>
    /// Builds a key set from SubjectPublicKeyInfo bytes, deep-copying every value and rejecting
    /// anything that is not a P-256 public key.
    /// </summary>
    internal static TrustedAuthorityKeySet Create(IEnumerable<KeyValuePair<string, byte[]>> spkiByKeyId)
    {
        ArgumentNullException.ThrowIfNull(spkiByKeyId);

        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var pair in spkiByKeyId)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("A trust-store key id must be nonempty.", nameof(spkiByKeyId));
            }

            ArgumentNullException.ThrowIfNull(pair.Value);

            var copy = (byte[])pair.Value.Clone();
            using var probe = ECDsa.Create();
            probe.ImportSubjectPublicKeyInfo(copy, out var read);

            if (probe.KeySize != 256 || read != copy.Length)
            {
                throw new ArgumentException(
                    $"Trust-store key '{pair.Key}' must be a P-256 SubjectPublicKeyInfo.", nameof(spkiByKeyId));
            }

            if (!map.TryAdd(pair.Key, copy))
            {
                throw new ArgumentException($"Duplicate trust-store key id '{pair.Key}'.", nameof(spkiByKeyId));
            }
        }

        return new TrustedAuthorityKeySet(map);
    }

    /// <summary>
    /// Resolves a key id to a fresh <see cref="ECDsa"/> instance over a private copy of its public
    /// key. The caller owns and disposes it. Returns <c>false</c> for an unknown key id.
    /// </summary>
    internal bool TryResolve(string keyId, out ECDsa? key)
    {
        key = null;

        if (string.IsNullOrEmpty(keyId) || !_spkiByKeyId.TryGetValue(keyId, out var spki))
        {
            return false;
        }

        var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
        key = ecdsa;
        return true;
    }
}

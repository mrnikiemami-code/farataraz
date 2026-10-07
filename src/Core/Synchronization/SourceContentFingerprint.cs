namespace FaraTaraz.Core.Synchronization;

using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Deterministic content fingerprint used as one optional change signal (see
/// <see cref="SourceRecordVersion"/>).
///
/// Guarantees:
/// - deterministic: identical canonical content yields identical output;
/// - canonical: object members are sorted by property name and formatting is fixed, so the
///   result is independent of input ordering (e.g. dictionary insertion order);
/// - no runtime-random input;
/// - does NOT include any platform retrieval timestamp (that is provenance, not identity).
///
/// The fingerprint is OPTIONAL. Providers that do not expose a revision or modification
/// time may still supply a content fingerprint, or none at all. This does not overcommit
/// to a single provider mechanism.
/// </summary>
public static class SourceContentFingerprint
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Computes a stable SHA-256 hex fingerprint over the canonical JSON representation of
    /// <paramref name="canonicalContent"/>. Callers should pass provider-agnostic business
    /// content (records or anonymous objects with only business fields).
    /// </summary>
    public static string Compute(object canonicalContent)
    {
        var root = JsonNode.Parse(JsonSerializer.Serialize(canonicalContent, CanonicalOptions))!;
        var canonical = Canonicalize(root);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Produces the canonical JSON string for a node without mutating the tree: object members
    /// are emitted in sorted property-name order, arrays keep their order (order is meaningful
    /// for sequences, unlike object members), and scalars are serialized as-is.
    /// </summary>
    private static string Canonicalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var parts = new List<string>();
            foreach (var kvp in obj.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                parts.Add(JsonSerializer.Serialize(kvp.Key, CanonicalOptions));
                parts.Add(":");
                parts.Add(Canonicalize(kvp.Value!));
            }

            return "{" + string.Join("", parts) + "}";
        }

        if (node is JsonArray arr)
        {
            var parts = arr.Select(item => Canonicalize(item!)).ToArray();
            return "[" + string.Join(",", parts) + "]";
        }

        return node.ToJsonString(CanonicalOptions);
    }
}

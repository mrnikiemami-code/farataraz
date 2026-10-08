namespace FaraTaraz.SyncContracts.Tests;

using System.Security.Cryptography;
using System.Text;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Xunit;

/// <summary>
/// Focused evidence for <see cref="SourceContentFingerprint"/> canonicalization (W1-R3-A).
///
/// These three tests pin the canonical contract: object members are order-independent and
/// sorted, arrays and nulls are handled deterministically, and distinct content yields
/// distinct fingerprints. The golden-hash assertions compare <c>Compute</c> against the
/// SHA-256 of the KNOWN canonical JSON, so they fail if the canonical form regresses
/// (e.g. a missing property separator) or throws on null.
/// </summary>
public class SourceContentFingerprintTests
{
    [Fact]
    public void Same_content_different_property_order_produces_same_fingerprint()
    {
        var ordered = SourceContentFingerprint.Compute(new { a = 1, b = 2, c = 3 });
        var reshuffled = SourceContentFingerprint.Compute(new { c = 3, a = 1, b = 2 });

        Assert.Equal(ordered, reshuffled);
    }

    [Fact]
    public void Null_object_property_and_null_array_element_are_deterministic_without_exception()
    {
        var payload = new
        {
            items = new object?[] { null, 1, null },
            label = (string?)null
        };

        var first = SourceContentFingerprint.Compute(payload);
        var second = SourceContentFingerprint.Compute(payload);

        // No NullReferenceException, and the result is stable across repeated computation.
        Assert.Equal(second, first);

        // The canonical form must carry the explicit JSON null literals.
        Assert.Equal(
            CanonicalSha256("{\"items\":[null,1,null],\"label\":null}"),
            first);
    }

    [Fact]
    public void Different_multi_property_objects_produce_different_fingerprints()
    {
        var one = SourceContentFingerprint.Compute(new { a = 1, b = 2 });
        var two = SourceContentFingerprint.Compute(new { a = 1, b = 3 });
        var three = SourceContentFingerprint.Compute(new { a = 2, b = 2 });

        Assert.NotEqual(one, two);
        Assert.NotEqual(one, three);
        Assert.NotEqual(two, three);

        // The multi-property shape is exactly where a missing property separator would corrupt
        // the canonical JSON; pin the fingerprint to the valid canonical form.
        Assert.Equal(CanonicalSha256("{\"a\":1,\"b\":2}"), one);
    }

    private static string CanonicalSha256(string canonicalJson)
    {
        var bytes = Encoding.UTF8.GetBytes(canonicalJson);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

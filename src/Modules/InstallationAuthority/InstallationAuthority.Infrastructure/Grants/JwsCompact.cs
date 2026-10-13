namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using System.Text;

/// <summary>
/// Strict compact-JWS lexical handling: bounded ASCII validation, exactly-three-segment splitting
/// and a self-contained strict base64url decoder. It performs no JSON work and no cryptography.
///
/// The decoder is strict at every call site (outer JWS segments and nested claim encodings alike):
/// it rejects padding, whitespace, <c>+</c>, <c>/</c>, characters outside the base64url alphabet,
/// impossible lengths and noncanonical unused pad bits. It never delegates to
/// <see cref="Convert.FromBase64String(string)"/>, which accepts <c>+</c>/<c>/</c>/padding and
/// ignores noncanonical trailing bits.
/// </summary>
internal static class JwsCompact
{
    /// <summary>Hard upper bound on the whole grant. Oversize input is rejected before any parse.</summary>
    internal const int MaxGrantBytes = 8 * 1024;

    /// <summary>Base64url alphabet plus the two compact separators; every other byte is rejected.</summary>
    internal static bool IsCompactAscii(ReadOnlySpan<byte> value)
    {
        foreach (var b in value)
        {
            var ok = b is >= (byte)'A' and <= (byte)'Z'
                or >= (byte)'a' and <= (byte)'z'
                or >= (byte)'0' and <= (byte)'9'
                or (byte)'-' or (byte)'_' or (byte)'.';

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Splits a compact JWS into its three non-empty segments, rejecting extra segments, missing
    /// segments and empty segments.
    /// </summary>
    internal static bool TrySplitSegments(
        string text, out string headerSegment, out string payloadSegment, out string signatureSegment)
    {
        headerSegment = payloadSegment = signatureSegment = string.Empty;

        var firstDot = text.IndexOf('.');
        var secondDot = firstDot < 0 ? -1 : text.IndexOf('.', firstDot + 1);

        if (firstDot <= 0 || secondDot <= firstDot + 1 || secondDot == text.Length - 1 ||
            text.IndexOf('.', secondDot + 1) >= 0)
        {
            return false;
        }

        headerSegment = text[..firstDot];
        payloadSegment = text[(firstDot + 1)..secondDot];
        signatureSegment = text[(secondDot + 1)..];
        return true;
    }

    /// <summary>
    /// Strict, canonical base64url decode: no padding, no whitespace, no <c>+</c>/<c>/</c>, no
    /// characters outside the alphabet, no impossible length, and zero unused pad bits in the
    /// final character.
    /// </summary>
    internal static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var length = value.Length;
        var remainder = length % 4;

        // A single trailing character can never encode a whole byte.
        if (remainder == 1)
        {
            return false;
        }

        // Canonical encoding: the unused pad bits of the final character must be zero.
        if (remainder == 2 && (DecodeChar(value[length - 1]) & 0x0F) != 0)
        {
            return false;
        }

        if (remainder == 3 && (DecodeChar(value[length - 1]) & 0x03) != 0)
        {
            return false;
        }

        var output = new byte[length / 4 * 3 + (remainder == 2 ? 1 : remainder == 3 ? 2 : 0)];
        var buffer = 0;
        var bitCount = 0;
        var index = 0;

        foreach (var c in value)
        {
            var decoded = DecodeChar(c);
            if (decoded < 0)
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            buffer = (buffer << 6) | decoded;
            bitCount += 6;

            if (bitCount >= 8)
            {
                bitCount -= 8;
                output[index++] = (byte)(buffer >> bitCount);
            }
        }

        // Reject anything whose pad bits were not zero (defence in depth; the checks above are the
        // canonical gate, this catches a leftover partial group).
        if (bitCount > 0 && (buffer & ((1 << bitCount) - 1)) != 0)
        {
            return false;
        }

        bytes = output;
        return true;
    }

    /// <summary>Six-bit value for a base64url character, or <c>-1</c> for any other character.</summary>
    private static int DecodeChar(char c) => c switch
    {
        >= 'A' and <= 'Z' => c - 'A',
        >= 'a' and <= 'z' => c - 'a' + 26,
        >= '0' and <= '9' => c - '0' + 52,
        '-' => 62,
        '_' => 63,
        _ => -1,
    };

    /// <summary>The exact signing input: the ORIGINAL encoded segments, never a re-serialised JSON.</summary>
    internal static byte[] SigningInput(string headerSegment, string payloadSegment)
        => Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment);
}

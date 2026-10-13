namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using System.Text;

/// <summary>
/// Strict compact-JWS lexical handling: bounded ASCII validation, exactly-three-segment splitting
/// and strict base64url decoding. It performs no JSON work and no cryptography.
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
    /// Strict base64url decode: no padding, no whitespace, no characters outside the alphabet.
    /// </summary>
    internal static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        if (value.Length == 0)
        {
            return false;
        }

        var padded = value.Replace('-', '+').Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 0:
                break;
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
            default:
                return false; // length % 4 == 1 can never be valid base64
        }

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>The exact signing input: the ORIGINAL encoded segments, never a re-serialised JSON.</summary>
    internal static byte[] SigningInput(string headerSegment, string payloadSegment)
        => Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment);
}

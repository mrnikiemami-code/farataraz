namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using System.Buffers.Binary;
using System.Text;

/// <summary>
/// The ONE canonical byte encoding of the possession message the installation must sign.
///
/// Every field is length-prefixed (four-byte big-endian UTF-8 length, then the UTF-8 bytes), in a
/// fixed order, starting with a fixed protocol identifier. Variable-length strings are never
/// concatenated without boundaries, so no two distinct field tuples can produce the same bytes.
///
/// Order: protocol identifier, grant id, tenant id, installation id, source id, capability,
/// nonce (also length-prefixed; 32 random bytes in production).
/// </summary>
internal static class PossessionMessage
{
    /// <summary>Fixed protocol/domain separator bound into every signed message.</summary>
    internal const string ProtocolIdentifier = "FT-INSTALLATION-POSSESSION-V1";

    internal static byte[] Encode(
        string grantId,
        string tenantId,
        string installationId,
        string sourceId,
        string capability,
        byte[] nonce)
    {
        ArgumentNullException.ThrowIfNull(nonce);

        using var buffer = new MemoryStream();

        WriteField(buffer, ProtocolIdentifier);
        WriteField(buffer, grantId);
        WriteField(buffer, tenantId);
        WriteField(buffer, installationId);
        WriteField(buffer, sourceId);
        WriteField(buffer, capability);
        WriteLengthPrefixed(buffer, nonce);

        return buffer.ToArray();
    }

    private static void WriteField(Stream stream, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteLengthPrefixed(stream, Encoding.UTF8.GetBytes(value));
    }

    private static void WriteLengthPrefixed(Stream stream, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)value.Length);
        stream.Write(length);
        stream.Write(value);
    }
}

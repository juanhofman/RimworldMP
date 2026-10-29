using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace RimworldMP.ModSync.Protocol;

/// <summary>
/// Length-prefixed framed protocol for LAN mod sync (TCP).
/// Frame: [4-byte BE length including opcode][1-byte opcode][payload…].
/// Default bind: LAN / loopback — not a WAN service.
/// </summary>
public static class ModSyncProtocol
{
    public const int Version = 1;
    public const int DefaultPort = 30557;
    public const string Magic = "RMPS";

    /// <summary>Max single frame payload (64 MiB) — large mods over LAN.</summary>
    public const int MaxFrameBytes = 64 * 1024 * 1024;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}

public enum ModSyncOpcode : byte
{
    Hello = 0x01,
    HelloAck = 0x02,
    RequestManifest = 0x10,
    Manifest = 0x11,
    RequestMod = 0x20,
    ModBlob = 0x21,
    Error = 0x7F,
}

public static class ModSyncFrameIo
{
    public static async Task WriteFrameAsync(
        NetworkStream stream,
        ModSyncOpcode opcode,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.Length + 1 > ModSyncProtocol.MaxFrameBytes)
            throw new InvalidOperationException("Frame too large.");

        var len = 1 + payload.Length;
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, len);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(new byte[] { (byte)opcode }, cancellationToken).ConfigureAwait(false);
        if (payload.Length > 0)
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<(ModSyncOpcode Opcode, byte[] Payload)> ReadFrameAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default)
    {
        var header = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        var len = BinaryPrimitives.ReadInt32BigEndian(header);
        if (len < 1 || len > ModSyncProtocol.MaxFrameBytes)
            throw new InvalidOperationException($"Invalid frame length: {len}");

        var body = await ReadExactAsync(stream, len, cancellationToken).ConfigureAwait(false);
        var opcode = (ModSyncOpcode)body[0];
        var payload = body.Length == 1 ? Array.Empty<byte>() : body[1..];
        return (opcode, payload);
    }

    public static byte[] EncodeHello()
    {
        var magic = Encoding.ASCII.GetBytes(ModSyncProtocol.Magic);
        var buf = new byte[magic.Length + 1];
        Buffer.BlockCopy(magic, 0, buf, 0, magic.Length);
        buf[^1] = (byte)ModSyncProtocol.Version;
        return buf;
    }

    public static void ValidateHello(ReadOnlySpan<byte> payload)
    {
        var magic = Encoding.ASCII.GetBytes(ModSyncProtocol.Magic);
        if (payload.Length < magic.Length + 1)
            throw new InvalidOperationException("Hello too short.");
        if (!payload[..magic.Length].SequenceEqual(magic))
            throw new InvalidOperationException("Bad mod-sync magic.");
        var ver = payload[magic.Length];
        if (ver != ModSyncProtocol.Version)
            throw new InvalidOperationException($"Unsupported mod-sync protocol version {ver}.");
    }

    public static byte[] EncodeUtf8(string s) => Encoding.UTF8.GetBytes(s);

    public static string DecodeUtf8(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);

    public static byte[] EncodeManifest(SessionModManifest manifest) =>
        JsonSerializer.SerializeToUtf8Bytes(manifest, ModSyncProtocol.JsonOptions);

    public static SessionModManifest DecodeManifest(ReadOnlySpan<byte> bytes) =>
        JsonSerializer.Deserialize<SessionModManifest>(bytes, ModSyncProtocol.JsonOptions)
        ?? throw new InvalidOperationException("Null manifest.");

    /// <summary>ModBlob payload: [2 BE packageIdLen][packageId utf8][zip bytes…]</summary>
    public static byte[] EncodeModBlob(string packageId, byte[] zipBytes)
    {
        var idBytes = Encoding.UTF8.GetBytes(packageId);
        if (idBytes.Length > ushort.MaxValue)
            throw new InvalidOperationException("packageId too long.");
        var buf = new byte[2 + idBytes.Length + zipBytes.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0, 2), (ushort)idBytes.Length);
        Buffer.BlockCopy(idBytes, 0, buf, 2, idBytes.Length);
        Buffer.BlockCopy(zipBytes, 0, buf, 2 + idBytes.Length, zipBytes.Length);
        return buf;
    }

    public static (string PackageId, byte[] ZipBytes) DecodeModBlob(byte[] payload)
    {
        if (payload.Length < 2)
            throw new InvalidOperationException("ModBlob too short.");
        var idLen = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2));
        if (2 + idLen > payload.Length)
            throw new InvalidOperationException("ModBlob packageId length overflow.");
        var packageId = Encoding.UTF8.GetString(payload, 2, idLen);
        var zip = payload[(2 + idLen)..];
        return (packageId, zip);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken ct)
    {
        var buf = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var n = await stream.ReadAsync(buf.AsMemory(offset, count - offset), ct).ConfigureAwait(false);
            if (n == 0)
                throw new EndOfStreamException("Connection closed while reading frame.");
            offset += n;
        }

        return buf;
    }
}

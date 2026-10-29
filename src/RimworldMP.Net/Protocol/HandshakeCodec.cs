using System.Buffers.Binary;
using System.Text;

namespace RimworldMP.Net.Protocol;

/// <summary>
/// Shared handshake / join-reject encode-decode for TCP and LiteNetLib transports.
/// Protocol v4 appends an optional trailer after the modlist hash; v3 peers ignore it.
/// </summary>
public static class HandshakeCodec
{
    /// <summary>Flag: peer supports paced JIP / large-payload scheduling.</summary>
    public const byte FlagPacingCapable = 1 << 0;

    public static byte[] BuildHandshakePayload(
        byte version,
        string modlistHash,
        NetTransportKind transportKind = NetTransportKind.Tcp,
        byte flags = 0)
    {
        var hashBytes = Encoding.UTF8.GetBytes(modlistHash ?? string.Empty);
        if (hashBytes.Length > 65535)
            hashBytes = hashBytes.AsSpan(0, 65535).ToArray();

        var trailer = version >= 4 ? 2 : 0;
        var buf = new byte[NetProtocol.HandshakeMagic.Length + 1 + 2 + hashBytes.Length + trailer];
        NetProtocol.HandshakeMagic.CopyTo(buf, 0);
        buf[NetProtocol.HandshakeMagic.Length] = version;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(NetProtocol.HandshakeMagic.Length + 1), (ushort)hashBytes.Length);
        hashBytes.CopyTo(buf.AsSpan(NetProtocol.HandshakeMagic.Length + 3));
        if (trailer > 0)
        {
            var off = NetProtocol.HandshakeMagic.Length + 3 + hashBytes.Length;
            buf[off] = (byte)transportKind;
            buf[off + 1] = flags;
        }

        return buf;
    }

    public static string ReadModlistHash(ReadOnlySpan<byte> payload)
    {
        var min = NetProtocol.HandshakeMagic.Length + 1 + 2;
        if (payload.Length < min)
            return string.Empty;
        var hashLen = BinaryPrimitives.ReadUInt16LittleEndian(payload[(NetProtocol.HandshakeMagic.Length + 1)..]);
        var start = NetProtocol.HandshakeMagic.Length + 3;
        if (payload.Length < start + hashLen)
            return string.Empty;
        return hashLen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(start, hashLen));
    }

    public static bool TryReadTrailer(
        ReadOnlySpan<byte> payload,
        out NetTransportKind transportKind,
        out byte flags)
    {
        transportKind = NetTransportKind.Tcp;
        flags = 0;
        var min = NetProtocol.HandshakeMagic.Length + 1 + 2;
        if (payload.Length < min)
            return false;
        var hashLen = BinaryPrimitives.ReadUInt16LittleEndian(payload[(NetProtocol.HandshakeMagic.Length + 1)..]);
        var start = NetProtocol.HandshakeMagic.Length + 3 + hashLen;
        if (payload.Length < start + 2)
            return false;
        transportKind = (NetTransportKind)payload[start];
        flags = payload[start + 1];
        return true;
    }

    public static bool HasValidMagic(ReadOnlySpan<byte> payload) =>
        payload.Length >= NetProtocol.HandshakeMagic.Length
        && payload[..NetProtocol.HandshakeMagic.Length].SequenceEqual(NetProtocol.HandshakeMagic);

    public static byte ReadRemoteVersion(ReadOnlySpan<byte> payload) =>
        payload.Length > NetProtocol.HandshakeMagic.Length
            ? payload[NetProtocol.HandshakeMagic.Length]
            : (byte)0;

    public static byte[] BuildJoinAck(int peerId, long sessionSeed)
    {
        var ack = new byte[13];
        ack[0] = NetProtocol.Version;
        BinaryPrimitives.WriteInt32LittleEndian(ack.AsSpan(1), peerId);
        BinaryPrimitives.WriteInt64LittleEndian(ack.AsSpan(5), sessionSeed);
        return ack;
    }

    public static bool TryParseJoinAck(ReadOnlySpan<byte> payload, out byte hostVersion, out int peerId, out long sessionSeed)
    {
        hostVersion = 0;
        peerId = 0;
        sessionSeed = 0;
        if (payload.Length < 13)
            return false;
        hostVersion = payload[0];
        peerId = BinaryPrimitives.ReadInt32LittleEndian(payload[1..]);
        sessionSeed = BinaryPrimitives.ReadInt64LittleEndian(payload[5..]);
        return true;
    }

    public static byte[] BuildJoinReject(JoinRejectReason reason, string message)
    {
        var msgBytes = Encoding.UTF8.GetBytes(message ?? string.Empty);
        if (msgBytes.Length > 1024)
            msgBytes = msgBytes.AsSpan(0, 1024).ToArray();
        var buf = new byte[1 + 2 + msgBytes.Length];
        buf[0] = (byte)reason;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(1), (ushort)msgBytes.Length);
        msgBytes.CopyTo(buf.AsSpan(3));
        return buf;
    }

    public static void ParseJoinReject(ReadOnlySpan<byte> payload, out JoinRejectReason reason, out string message)
    {
        if (payload.Length < 3)
        {
            reason = JoinRejectReason.Unspecified;
            message = "rejected";
            return;
        }

        reason = (JoinRejectReason)payload[0];
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[1..]);
        message = payload.Length < 3 + len || len == 0
            ? reason.ToString()
            : Encoding.UTF8.GetString(payload.Slice(3, len));
    }

    public static string Truncate(string? value)
    {
        value ??= string.Empty;
        return value.Length <= 16 ? value : value[..16] + "…";
    }
}

using System.Buffers.Binary;

namespace RimworldMP.Core.Commands;

/// <summary>Single lockstep command: opcode + scheduled tick + issuing player + opaque payload.</summary>
public readonly struct MpCommand
{
    public MpCommand(CommandOpcode opcode, long tick, int playerId, byte[]? payload = null)
    {
        Opcode = opcode;
        Tick = tick;
        PlayerId = playerId;
        Payload = payload ?? Array.Empty<byte>();
    }

    public CommandOpcode Opcode { get; }
    public long Tick { get; }
    public int PlayerId { get; }
    public byte[] Payload { get; }

    public int SerializedSize => 2 + 8 + 4 + 4 + Payload.Length;

    public void WriteTo(Span<byte> dest)
    {
        if (dest.Length < SerializedSize)
            throw new ArgumentException("Destination too small.", nameof(dest));

        BinaryPrimitives.WriteUInt16LittleEndian(dest, (ushort)Opcode);
        BinaryPrimitives.WriteInt64LittleEndian(dest[2..], Tick);
        BinaryPrimitives.WriteInt32LittleEndian(dest[10..], PlayerId);
        BinaryPrimitives.WriteInt32LittleEndian(dest[14..], Payload.Length);
        if (Payload.Length > 0)
            Payload.CopyTo(dest[18..]);
    }

    public byte[] ToBytes()
    {
        var buffer = new byte[SerializedSize];
        WriteTo(buffer);
        return buffer;
    }

    public static MpCommand ReadFrom(ReadOnlySpan<byte> src, out int consumed)
    {
        if (src.Length < 18)
            throw new InvalidOperationException("Command buffer truncated.");

        var opcode = (CommandOpcode)BinaryPrimitives.ReadUInt16LittleEndian(src);
        var tick = BinaryPrimitives.ReadInt64LittleEndian(src[2..]);
        var playerId = BinaryPrimitives.ReadInt32LittleEndian(src[10..]);
        var payloadLen = BinaryPrimitives.ReadInt32LittleEndian(src[14..]);
        if (payloadLen < 0 || src.Length < 18 + payloadLen)
            throw new InvalidOperationException("Command payload truncated.");

        var payload = payloadLen == 0 ? Array.Empty<byte>() : src.Slice(18, payloadLen).ToArray();
        consumed = 18 + payloadLen;
        return new MpCommand(opcode, tick, playerId, payload);
    }
}

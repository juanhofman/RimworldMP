using System.Buffers.Binary;

namespace RimworldMP.Core.Commands;

/// <summary>Ordered command list for a single simulation tick (may be empty).</summary>
public sealed class CommandBatch
{
    public CommandBatch(long tick, IReadOnlyList<MpCommand> commands)
    {
        Tick = tick;
        Commands = commands;
    }

    public long Tick { get; }
    public IReadOnlyList<MpCommand> Commands { get; }

    public static CommandBatch Empty(long tick) => new(tick, Array.Empty<MpCommand>());

    public byte[] ToBytes()
    {
        var size = 8 + 4;
        foreach (var cmd in Commands)
            size += cmd.SerializedSize;

        var buffer = new byte[size];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, Tick);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8), Commands.Count);
        var offset = 12;
        foreach (var cmd in Commands)
        {
            cmd.WriteTo(buffer.AsSpan(offset));
            offset += cmd.SerializedSize;
        }

        return buffer;
    }

    public static CommandBatch FromBytes(ReadOnlySpan<byte> src)
    {
        if (src.Length < 12)
            throw new InvalidOperationException("Batch buffer truncated.");

        var tick = BinaryPrimitives.ReadInt64LittleEndian(src);
        var count = BinaryPrimitives.ReadInt32LittleEndian(src[8..]);
        if (count < 0)
            throw new InvalidOperationException("Negative command count.");

        var list = new List<MpCommand>(count);
        var offset = 12;
        for (var i = 0; i < count; i++)
        {
            var cmd = MpCommand.ReadFrom(src[offset..], out var consumed);
            list.Add(cmd);
            offset += consumed;
        }

        return new CommandBatch(tick, list);
    }
}

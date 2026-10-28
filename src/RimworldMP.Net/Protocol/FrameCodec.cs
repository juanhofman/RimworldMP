using System.Buffers;
using System.Buffers.Binary;

namespace RimworldMP.Net.Protocol;

/// <summary>Length-prefixed TCP frame encode/decode helpers.</summary>
public static class FrameCodec
{
    public const int HeaderSize = 5; // u32 length + u8 type

    public static byte[] Encode(NetMessageType type, ReadOnlySpan<byte> payload)
    {
        var length = 1 + payload.Length;
        if (length > NetProtocol.MaxFrameBytes)
            throw new InvalidOperationException($"Frame too large: {length}");

        var buffer = new byte[4 + length];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), (uint)length);
        buffer[4] = (byte)type;
        payload.CopyTo(buffer.AsSpan(5));
        return buffer;
    }

    public static bool TryDecode(ref ReadOnlySequence<byte> buffer, out NetMessageType type, out byte[] payload)
    {
        type = default;
        payload = Array.Empty<byte>();

        if (buffer.Length < 4)
            return false;

        Span<byte> lengthBytes = stackalloc byte[4];
        buffer.Slice(0, 4).CopyTo(lengthBytes);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
        if (length == 0 || length > NetProtocol.MaxFrameBytes)
            throw new InvalidOperationException($"Invalid frame length: {length}");

        if (buffer.Length < 4 + length)
            return false;

        var body = buffer.Slice(4, length);
        Span<byte> bodySpan = length <= 4096 ? stackalloc byte[(int)length] : new byte[length];
        body.CopyTo(bodySpan);

        type = (NetMessageType)bodySpan[0];
        payload = bodySpan.Length == 1 ? Array.Empty<byte>() : bodySpan.Slice(1).ToArray();
        buffer = buffer.Slice(4 + length);
        return true;
    }

    /// <summary>Read one complete frame from a stream (blocking / async).</summary>
    public static async Task<(NetMessageType Type, byte[] Payload)> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var header = new byte[4];
        await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length == 0 || length > NetProtocol.MaxFrameBytes)
            throw new InvalidOperationException($"Invalid frame length: {length}");

        var body = new byte[length];
        await ReadExactAsync(stream, body, cancellationToken).ConfigureAwait(false);
        return ((NetMessageType)body[0], body.Length == 1 ? Array.Empty<byte>() : body.AsSpan(1).ToArray());
    }

    public static async Task WriteFrameAsync(
        Stream stream,
        NetMessageType type,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        var frame = Encode(type, payload.Span);
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Remote closed the connection.");
            offset += read;
        }
    }
}

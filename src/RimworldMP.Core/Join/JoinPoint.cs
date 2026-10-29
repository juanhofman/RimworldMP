using System.Buffers.Binary;
using System.Text;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Join;

/// <summary>
/// Host-authored freeze-frame for join-in-progress: save blob + tick watermark + modlist hash.
/// </summary>
public sealed class JoinPoint
{
    public JoinPoint(
        long nextTick,
        long sessionSeed,
        long commandWatermark,
        string modlistHash,
        byte[] saveBlob)
    {
        NextTick = nextTick;
        SessionSeed = sessionSeed;
        CommandWatermark = commandWatermark;
        ModlistHash = modlistHash ?? string.Empty;
        SaveBlob = saveBlob ?? Array.Empty<byte>();
    }

    public long NextTick { get; }
    public long SessionSeed { get; }
    public long CommandWatermark { get; }
    public string ModlistHash { get; }
    public byte[] SaveBlob { get; }

    public byte[] ToHeaderBytes()
    {
        var hashBytes = Encoding.UTF8.GetBytes(ModlistHash);
        var buf = new byte[8 + 8 + 8 + 4 + 2 + hashBytes.Length];
        BinaryPrimitives.WriteInt64LittleEndian(buf, NextTick);
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(8), SessionSeed);
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(16), CommandWatermark);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(24), SaveBlob.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(28), (ushort)hashBytes.Length);
        hashBytes.CopyTo(buf.AsSpan(30));
        return buf;
    }

    public static (long NextTick, long SessionSeed, long Watermark, int BlobLength, string ModlistHash) ReadHeader(
        ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 30)
            throw new InvalidOperationException("JoinPoint header truncated.");
        var nextTick = BinaryPrimitives.ReadInt64LittleEndian(payload);
        var seed = BinaryPrimitives.ReadInt64LittleEndian(payload[8..]);
        var watermark = BinaryPrimitives.ReadInt64LittleEndian(payload[16..]);
        var blobLen = BinaryPrimitives.ReadInt32LittleEndian(payload[24..]);
        var hashLen = BinaryPrimitives.ReadUInt16LittleEndian(payload[28..]);
        if (payload.Length < 30 + hashLen)
            throw new InvalidOperationException("JoinPoint modlist hash truncated.");
        var hash = hashLen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(30, hashLen));
        return (nextTick, seed, watermark, blobLen, hash);
    }

    /// <summary>Split save blob into wire chunks: [i32 index][i32 total][bytes…].</summary>
    public IEnumerable<byte[]> EnumerateChunks(int chunkBytes = NetProtocol.JoinPointChunkBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkBytes);
        if (SaveBlob.Length == 0)
        {
            yield return PackChunk(0, 1, ReadOnlySpan<byte>.Empty);
            yield break;
        }

        var total = (SaveBlob.Length + chunkBytes - 1) / chunkBytes;
        for (var i = 0; i < total; i++)
        {
            var offset = i * chunkBytes;
            var len = Math.Min(chunkBytes, SaveBlob.Length - offset);
            yield return PackChunk(i, total, SaveBlob.AsSpan(offset, len));
        }
    }

    public static (int Index, int Total, byte[] Data) ReadChunk(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
            throw new InvalidOperationException("JoinPoint chunk truncated.");
        var index = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var total = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        var data = payload.Length == 8 ? Array.Empty<byte>() : payload[8..].ToArray();
        return (index, total, data);
    }

    private static byte[] PackChunk(int index, int total, ReadOnlySpan<byte> data)
    {
        var buf = new byte[8 + data.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, index);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), total);
        data.CopyTo(buf.AsSpan(8));
        return buf;
    }
}

/// <summary>Host-side factory for building a join point (game layer supplies the save blob).</summary>
public interface IJoinPointSource
{
    JoinPoint? TryCreateJoinPoint(long nextTick, long sessionSeed, long commandWatermark);
}

/// <summary>Simple in-memory source used by harnesses and DevMode stubs.</summary>
public sealed class MemoryJoinPointSource : IJoinPointSource
{
    private byte[] _blob;
    private string _modlistHash;

    public MemoryJoinPointSource(byte[] blob, string modlistHash = "test-modlist")
    {
        _blob = blob ?? Array.Empty<byte>();
        _modlistHash = modlistHash;
    }

    public void SetBlob(byte[] blob, string? modlistHash = null)
    {
        _blob = blob ?? Array.Empty<byte>();
        if (modlistHash is not null)
            _modlistHash = modlistHash;
    }

    public JoinPoint? TryCreateJoinPoint(long nextTick, long sessionSeed, long commandWatermark) =>
        new(nextTick, sessionSeed, commandWatermark, _modlistHash, _blob);
}

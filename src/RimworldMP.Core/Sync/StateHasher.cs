using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using RimworldMP.Core.Commands;

namespace RimworldMP.Core.Sync;

/// <summary>
/// Rolling command-stream fingerprint feeding <see cref="DesyncDetector"/> checkpoints.
/// Callers may also append tick / draw / protocol identity into the rolling hash; the
/// periodic snapshot additionally folds RNG counters + a reserved world-fingerprint slot.
/// </summary>
public sealed class StateHasher
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly object _gate = new();

    public void Reset()
    {
        lock (_gate)
        {
            _hash.GetHashAndReset();
        }
    }

    public void AddTick(long tick)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, tick);
        Append(buf);
    }

    public void AddDrawCount(long drawCount)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, drawCount);
        Append(buf);
    }

    /// <summary>Fold protocol version into the rolling stream (session start / join).</summary>
    public void AddProtocolVersion(byte version)
    {
        Span<byte> buf = stackalloc byte[1];
        buf[0] = version;
        Append(buf);
    }

    /// <summary>Fold session seed so peers with mismatched JoinAck seeds diverge immediately.</summary>
    public void AddSessionSeed(long seed)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, seed);
        Append(buf);
    }

    public void AddCommand(MpCommand command)
    {
        var bytes = command.ToBytes();
        Append(bytes);
    }

    public void AddBatch(CommandBatch batch)
    {
        AddTick(batch.Tick);
        foreach (var cmd in batch.Commands)
            AddCommand(cmd);
    }

    public void AddBytes(ReadOnlySpan<byte> data) => Append(data);

    public void AddUtf8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Append(bytes);
    }

    public byte[] CurrentHash()
    {
        lock (_gate)
        {
            // GetCurrentHash doesn't reset — clone via GetHashAndReset + re-feed is heavy;
            // use GetCurrentHash available on IncrementalHash in modern .NET.
            return _hash.GetCurrentHash();
        }
    }

    public string CurrentHashHex() => Convert.ToHexString(CurrentHash()).ToLowerInvariant();

    private void Append(ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            _hash.AppendData(data);
        }
    }
}

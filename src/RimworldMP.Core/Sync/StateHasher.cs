using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using RimworldMP.Core.Commands;

namespace RimworldMP.Core.Sync;

/// <summary>
/// Lightweight state fingerprint for desync detection. Without game refs this hashes
/// tick + RNG draw count + applied command stream. With game refs, callers can append map hashes.
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

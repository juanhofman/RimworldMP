using System.Buffers.Binary;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Join;

/// <summary>
/// Host→client chunked join-point transfer and client-side reassembly.
/// </summary>
public sealed class JoinPointTransfer
{
    private readonly object _gate = new();
    private long _expectedNextTick;
    private long _expectedSeed;
    private long _expectedWatermark;
    private int _expectedBlobLength;
    private string _expectedModlistHash = string.Empty;
    private byte[]? _buffer;
    private int _receivedChunks;
    private int _totalChunks = -1;
    private bool _headerReceived;

    public bool IsReceiving
    {
        get { lock (_gate) return _headerReceived && _buffer is not null; }
    }

    public JoinPoint? Completed { get; private set; }

    public void Reset()
    {
        lock (_gate)
        {
            _headerReceived = false;
            _buffer = null;
            _receivedChunks = 0;
            _totalChunks = -1;
            Completed = null;
        }
    }

    public void AcceptHeader(ReadOnlySpan<byte> payload)
    {
        var (next, seed, watermark, blobLen, hash) = JoinPoint.ReadHeader(payload);
        if (blobLen < 0 || blobLen > NetProtocol.MaxFrameBytes * 64)
            throw new InvalidOperationException($"JoinPoint blob too large: {blobLen}");

        lock (_gate)
        {
            _expectedNextTick = next;
            _expectedSeed = seed;
            _expectedWatermark = watermark;
            _expectedBlobLength = blobLen;
            _expectedModlistHash = hash;
            _buffer = blobLen == 0 ? Array.Empty<byte>() : new byte[blobLen];
            _receivedChunks = 0;
            _totalChunks = -1;
            _headerReceived = true;
            Completed = null;
        }
    }

    public void AcceptChunk(ReadOnlySpan<byte> payload)
    {
        var (index, total, data) = JoinPoint.ReadChunk(payload);
        lock (_gate)
        {
            if (!_headerReceived || _buffer is null)
                throw new InvalidOperationException("JoinPoint chunk before header.");
            if (_totalChunks < 0)
                _totalChunks = total;
            else if (_totalChunks != total)
                throw new InvalidOperationException("JoinPoint chunk total mismatch.");
            if (index < 0 || index >= total)
                throw new InvalidOperationException("JoinPoint chunk index out of range.");

            var offset = index * NetProtocol.JoinPointChunkBytes;
            if (offset + data.Length > _buffer.Length && _buffer.Length > 0)
                throw new InvalidOperationException("JoinPoint chunk overflows blob.");
            if (_buffer.Length > 0)
                data.CopyTo(_buffer.AsSpan(offset));
            _receivedChunks++;
        }
    }

    public JoinPoint? TryComplete()
    {
        lock (_gate)
        {
            if (!_headerReceived || _buffer is null || _totalChunks < 0)
                return null;
            if (_receivedChunks < _totalChunks)
                return null;

            Completed = new JoinPoint(
                _expectedNextTick,
                _expectedSeed,
                _expectedWatermark,
                _expectedModlistHash,
                _buffer);
            return Completed;
        }
    }

    public static void SendToPeer(
        INetTransport transport,
        NetPeerId peer,
        JoinPoint joinPoint,
        Func<NetMessageType, ReadOnlyMemory<byte>, byte[]> envelope)
    {
        transport.SendReliable(peer, envelope(NetMessageType.JoinPointHeader, joinPoint.ToHeaderBytes()));
        foreach (var chunk in joinPoint.EnumerateChunks())
            transport.SendReliable(peer, envelope(NetMessageType.JoinPointChunk, chunk));
        transport.SendReliable(peer, envelope(NetMessageType.JoinPointDone, Array.Empty<byte>()));
    }

    public static byte[] RequestBytes() => Array.Empty<byte>();
}

/// <summary>Ring of recent command batches for post-JIP catch-up past the save watermark.</summary>
public sealed class CommandLog
{
    private readonly Queue<CommandBatchBytes> _items = new();
    private readonly int _capacity;
    private readonly object _gate = new();

    public CommandLog(int capacity = 512)
    {
        _capacity = Math.Max(32, capacity);
    }

    public int Count
    {
        get { lock (_gate) return _items.Count; }
    }

    public void Record(long tick, byte[] batchBytes)
    {
        lock (_gate)
        {
            _items.Enqueue(new CommandBatchBytes(tick, batchBytes));
            while (_items.Count > _capacity)
                _items.Dequeue();
        }
    }

    /// <summary>Batches with tick &gt; watermark, ordered ascending.</summary>
    public List<byte[]> BatchesAfter(long watermark)
    {
        lock (_gate)
        {
            return _items
                .Where(i => i.Tick > watermark)
                .OrderBy(i => i.Tick)
                .Select(i => i.Bytes)
                .ToList();
        }
    }

    /// <summary>
    /// Last <paramref name="max"/> ring entries as desync-dump snapshots (ascending tick order).
    /// Used only for local diagnostics — never uploaded.
    /// </summary>
    public IReadOnlyList<Sync.DesyncBatchSnapshot> SnapshotRecent(int max = Sync.DesyncDump.DefaultBatchHistory)
    {
        lock (_gate)
        {
            if (max <= 0 || _items.Count == 0)
                return Array.Empty<Sync.DesyncBatchSnapshot>();

            var take = Math.Min(max, _items.Count);
            var skip = _items.Count - take;
            return _items
                .Skip(skip)
                .Select(i => Sync.DesyncDump.SnapshotBatch(i.Tick, i.Bytes))
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_gate) _items.Clear();
    }

    private readonly record struct CommandBatchBytes(long Tick, byte[] Bytes);
}

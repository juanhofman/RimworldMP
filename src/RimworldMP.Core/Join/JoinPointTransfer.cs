using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Join;

/// <summary>
/// Host→client chunked join-point transfer and client-side reassembly.
/// Incomplete or corrupt transfers fail loudly — never silently clear JIP state.
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
    private readonly HashSet<int> _receivedIndexes = new();
    private int _totalChunks = -1;
    private int _chunkBytes = NetProtocol.JoinPointChunkBytes;
    private bool _headerReceived;

    public bool IsReceiving
    {
        get { lock (_gate) return _headerReceived && _buffer is not null; }
    }

    public bool HeaderReceived
    {
        get { lock (_gate) return _headerReceived; }
    }

    public int ReceivedChunkCount
    {
        get { lock (_gate) return _receivedIndexes.Count; }
    }

    public int ExpectedChunkCount
    {
        get { lock (_gate) return _totalChunks; }
    }

    public JoinPoint? Completed { get; private set; }

    public void Reset()
    {
        lock (_gate)
        {
            _headerReceived = false;
            _buffer = null;
            _receivedIndexes.Clear();
            _totalChunks = -1;
            _chunkBytes = NetProtocol.JoinPointChunkBytes;
            Completed = null;
        }
    }

    public void AcceptHeader(ReadOnlySpan<byte> payload)
    {
        var (next, seed, watermark, blobLen, hash, chunkBytes) = JoinPoint.ReadHeader(payload);
        if (blobLen < 0 || blobLen > NetProtocol.MaxFrameBytes * 64)
            throw new InvalidOperationException($"JoinPoint blob too large: {blobLen}");

        lock (_gate)
        {
            _expectedNextTick = next;
            _expectedSeed = seed;
            _expectedWatermark = watermark;
            _expectedBlobLength = blobLen;
            _expectedModlistHash = hash;
            _chunkBytes = chunkBytes > 0 ? chunkBytes : NetProtocol.JoinPointChunkBytes;
            _buffer = blobLen == 0 ? Array.Empty<byte>() : new byte[blobLen];
            _receivedIndexes.Clear();
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
            if (total <= 0)
                throw new InvalidOperationException($"JoinPoint chunk total invalid: {total}");
            if (_totalChunks < 0)
                _totalChunks = total;
            else if (_totalChunks != total)
                throw new InvalidOperationException("JoinPoint chunk total mismatch.");
            if (index < 0 || index >= total)
                throw new InvalidOperationException("JoinPoint chunk index out of range.");

<<<<<<< HEAD
            var offset = index * NetProtocol.JoinPointChunkBytes;
            if (_buffer.Length == 0)
            {
                if (data.Length != 0)
                    throw new InvalidOperationException("JoinPoint empty blob received non-empty chunk.");
            }
            else
            {
                if (offset >= _buffer.Length)
                    throw new InvalidOperationException("JoinPoint chunk overflows blob.");
                var expectedLen = Math.Min(NetProtocol.JoinPointChunkBytes, _buffer.Length - offset);
                if (data.Length != expectedLen)
                    throw new InvalidOperationException(
                        $"JoinPoint chunk length mismatch at index {index}: got {data.Length}, expected {expectedLen}.");
=======
            var offset = index * _chunkBytes;
            if (offset + data.Length > _buffer.Length && _buffer.Length > 0)
                throw new InvalidOperationException("JoinPoint chunk overflows blob.");
            if (_buffer.Length > 0)
>>>>>>> 2ccd06b (Add LiteNetLib LAN transport behind INetTransport.)
                data.CopyTo(_buffer.AsSpan(offset));
            }

            // Duplicate index is fail-loud (silent overwrite would hide loss).
            if (!_receivedIndexes.Add(index))
                throw new InvalidOperationException($"JoinPoint duplicate chunk index {index}.");
        }
    }

    public JoinPoint? TryComplete()
    {
        lock (_gate)
        {
            if (!_headerReceived || _buffer is null || _totalChunks < 0)
                return null;
            if (_receivedIndexes.Count < _totalChunks)
                return null;
            // Ensure contiguous coverage 0..total-1
            for (var i = 0; i < _totalChunks; i++)
            {
                if (!_receivedIndexes.Contains(i))
                    return null;
            }

            Completed = new JoinPoint(
                _expectedNextTick,
                _expectedSeed,
                _expectedWatermark,
                _expectedModlistHash,
                _buffer);
            return Completed;
        }
    }

    /// <summary>
    /// Finish transfer after JoinPointDone. Throws if header was seen but reassembly is incomplete
    /// or corrupt — callers must not treat that as an empty lobby hello.
    /// </summary>
    public JoinPoint CompleteOrThrow()
    {
        lock (_gate)
        {
            if (!_headerReceived)
                throw new InvalidOperationException("JoinPointDone without header.");
            if (_buffer is null)
                throw new InvalidOperationException("JoinPointDone with null buffer.");
            if (_totalChunks < 0)
                throw new InvalidOperationException("JoinPointDone with no chunks received.");
            if (_receivedIndexes.Count != _totalChunks)
                throw new InvalidOperationException(
                    $"JoinPoint incomplete: received {_receivedIndexes.Count}/{_totalChunks} chunks " +
                    $"(blobLen={_expectedBlobLength}).");

            for (var i = 0; i < _totalChunks; i++)
            {
                if (!_receivedIndexes.Contains(i))
                    throw new InvalidOperationException($"JoinPoint missing chunk index {i}.");
            }

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
        var remoteVersion = transport switch
        {
            LiteNetLibTransport lnl => lnl.RemoteProtocolVersion,
            LanTcpTransport tcp => tcp.RemoteProtocolVersion,
            _ => NetProtocol.Version,
        };
        var chunkBytes = PayloadPacing.ResolveChunkBytes(transport, remoteVersion);
        var header = joinPoint.ToHeaderBytes(chunkBytes);
        transport.SendReliable(peer, envelope(NetMessageType.JoinPointHeader, header));

        var chunkEnvelopes = new List<ReadOnlyMemory<byte>>();
        foreach (var chunk in joinPoint.EnumerateChunks(chunkBytes))
            chunkEnvelopes.Add(envelope(NetMessageType.JoinPointChunk, chunk));

        PayloadPacing.SendPaced(transport, peer, chunkEnvelopes);
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

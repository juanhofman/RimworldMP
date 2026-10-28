using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Net;

/// <summary>
/// LAN TCP transport: length-prefixed frames, protocol version handshake, one-host / N-client.
/// Phase 1 targets host + one client on the same machine or LAN.
/// </summary>
public sealed class LanTcpTransport : INetTransport
{
    private enum Mode
    {
        Idle,
        Host,
        Client,
    }

    private Mode _mode = Mode.Idle;
    private NetEndpoint? _endpoint;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private readonly ConcurrentDictionary<NetPeerId, PeerConnection> _peers = new();
    private int _nextPeerId = 1;
    private readonly object _sendLock = new();

    public bool IsRunning => _mode != Mode.Idle;
    public NetEndpoint? Endpoint => _endpoint;
    public IReadOnlyCollection<NetPeerId> ConnectedPeers => _peers.Keys.ToArray();

    public event Action<NetPeerId, ReadOnlyMemory<byte>>? OnReceive;
    public event Action<NetPeerId>? OnPeerJoin;
    public event Action<NetPeerId>? OnPeerLeave;

    public async Task StartHostAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (_mode != Mode.Idle)
            throw new InvalidOperationException("Transport already running.");

        _endpoint = endpoint;
        _mode = Mode.Host;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var listener = new TcpListener(IPAddress.Any, endpoint.Port);
        listener.Start();
        _listener = listener;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StartClientAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (_mode != Mode.Idle)
            throw new InvalidOperationException("Transport already running.");

        _endpoint = endpoint;
        _mode = Mode.Client;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var client = new TcpClient();
        await client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        var stream = client.GetStream();

        // Client → host handshake: magic + version
        var handshake = new byte[NetProtocol.HandshakeMagic.Length + 1];
        NetProtocol.HandshakeMagic.CopyTo(handshake, 0);
        handshake[^1] = NetProtocol.Version;
        await FrameCodec.WriteFrameAsync(stream, NetMessageType.Handshake, handshake, cancellationToken)
            .ConfigureAwait(false);

        var (type, payload) = await FrameCodec.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
        if (type != NetMessageType.JoinAck || payload.Length < 5)
        {
            client.Dispose();
            throw new InvalidOperationException("Host rejected handshake / join.");
        }

        if (payload[0] != NetProtocol.Version)
        {
            client.Dispose();
            throw new InvalidOperationException($"Protocol version mismatch: host={payload[0]} local={NetProtocol.Version}");
        }

        var assignedId = BitConverter.ToInt32(payload, 1);
        var peerId = new NetPeerId(assignedId);
        var conn = new PeerConnection(peerId, client, stream);
        if (!_peers.TryAdd(NetPeerId.Host, conn))
        {
            client.Dispose();
            throw new InvalidOperationException("Failed to register host peer.");
        }

        OnPeerJoin?.Invoke(NetPeerId.Host);
        _ = Task.Run(() => ReceiveLoopAsync(conn, _cts.Token), CancellationToken.None);
    }

    public void SendReliable(NetPeerId peer, ReadOnlyMemory<byte> payload)
    {
        if (!IsRunning)
            throw new InvalidOperationException("Transport is not running.");

        if (!_peers.TryGetValue(peer, out var conn))
            throw new InvalidOperationException($"Unknown peer {peer}.");

        WriteTypedOrBatch(conn, payload);
    }

    public void BroadcastReliable(ReadOnlyMemory<byte> payload)
    {
        if (!IsRunning)
            throw new InvalidOperationException("Transport is not running.");

        foreach (var conn in _peers.Values)
            WriteTypedOrBatch(conn, payload);
    }

    /// <summary>
    /// Session layer sends <c>[NetMessageType][payload]</c>. Split that into a TCP frame type + body.
    /// Untyped blobs are treated as CommandBatch bodies (legacy / harness).
    /// </summary>
    private void WriteTypedOrBatch(PeerConnection conn, ReadOnlyMemory<byte> payload)
    {
        if (payload.Length >= 1 && IsKnownType(payload.Span[0]))
            WriteFrame(conn, (NetMessageType)payload.Span[0], payload[1..]);
        else
            WriteFrame(conn, NetMessageType.CommandBatch, payload);
    }

    private static bool IsKnownType(byte value) =>
        value is >= (byte)NetMessageType.Handshake and <= (byte)NetMessageType.KeepAlive;

    /// <summary>Send a typed control message to one peer (hash / desync / keepalive).</summary>
    public void SendControl(NetPeerId peer, NetMessageType type, ReadOnlyMemory<byte> payload)
    {
        if (!_peers.TryGetValue(peer, out var conn))
            throw new InvalidOperationException($"Unknown peer {peer}.");
        WriteFrame(conn, type, payload);
    }

    public void BroadcastControl(NetMessageType type, ReadOnlyMemory<byte> payload)
    {
        foreach (var conn in _peers.Values)
            WriteFrame(conn, type, payload);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_mode == Mode.Idle)
            return;

        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignore
        }

        _listener?.Stop();
        _listener = null;

        if (_acceptLoop is not null)
        {
            try
            {
                await Task.WhenAny(_acceptLoop, Task.Delay(500, cancellationToken)).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        foreach (var peer in _peers.Keys.ToArray())
            RemovePeer(peer);

        _cts?.Dispose();
        _cts = null;
        _acceptLoop = null;
        _mode = Mode.Idle;
        _endpoint = null;
        _nextPeerId = 1;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    public override string ToString() =>
        _mode == Mode.Idle ? "LanTcpTransport(idle)" : $"LanTcpTransport({_mode} {_endpoint} peers={_peers.Count})";

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                var stream = client.GetStream();
                var (type, payload) = await FrameCodec.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (type != NetMessageType.Handshake
                    || payload.Length < NetProtocol.HandshakeMagic.Length + 1
                    || !payload.AsSpan(0, NetProtocol.HandshakeMagic.Length).SequenceEqual(NetProtocol.HandshakeMagic))
                {
                    client.Dispose();
                    continue;
                }

                var remoteVersion = payload[NetProtocol.HandshakeMagic.Length];
                if (remoteVersion != NetProtocol.Version)
                {
                    client.Dispose();
                    continue;
                }

                var peerId = new NetPeerId(Interlocked.Increment(ref _nextPeerId));
                var ack = new byte[5];
                ack[0] = NetProtocol.Version;
                BitConverter.TryWriteBytes(ack.AsSpan(1), peerId.Value);
                await FrameCodec.WriteFrameAsync(stream, NetMessageType.JoinAck, ack, cancellationToken)
                    .ConfigureAwait(false);

                var conn = new PeerConnection(peerId, client, stream);
                if (!_peers.TryAdd(peerId, conn))
                {
                    client.Dispose();
                    continue;
                }

                client = null; // ownership transferred
                OnPeerJoin?.Invoke(peerId);
                _ = Task.Run(() => ReceiveLoopAsync(conn, cancellationToken), CancellationToken.None);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                client?.Dispose();
            }
        }
    }

    private async Task ReceiveLoopAsync(PeerConnection conn, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var (type, payload) = await FrameCodec.ReadFrameAsync(conn.Stream, cancellationToken)
                    .ConfigureAwait(false);

                // Surface all application payloads to the session layer with a 1-byte type prefix
                // so Core can demux without depending on NetMessageType for every path.
                var envelope = new byte[1 + payload.Length];
                envelope[0] = (byte)type;
                if (payload.Length > 0)
                    payload.CopyTo(envelope.AsSpan(1));
                OnReceive?.Invoke(conn.Id, envelope);
            }
        }
        catch
        {
            // disconnect
        }
        finally
        {
            RemovePeer(conn.Id);
        }
    }

    private void WriteFrame(PeerConnection conn, NetMessageType type, ReadOnlyMemory<byte> payload)
    {
        lock (_sendLock)
        {
            try
            {
                var frame = FrameCodec.Encode(type, payload.Span);
                conn.Stream.Write(frame);
                conn.Stream.Flush();
            }
            catch
            {
                RemovePeer(conn.Id);
            }
        }
    }

    private void RemovePeer(NetPeerId peer)
    {
        if (_peers.TryRemove(peer, out var conn))
        {
            try
            {
                conn.Dispose();
            }
            catch
            {
                // ignore
            }

            OnPeerLeave?.Invoke(peer);
        }
    }

    private sealed class PeerConnection : IDisposable
    {
        public PeerConnection(NetPeerId id, TcpClient client, NetworkStream stream)
        {
            Id = id;
            Client = client;
            Stream = stream;
        }

        public NetPeerId Id { get; }
        public TcpClient Client { get; }
        public NetworkStream Stream { get; }

        public void Dispose()
        {
            try { Stream.Dispose(); } catch { /* ignore */ }
            try { Client.Dispose(); } catch { /* ignore */ }
        }
    }
}

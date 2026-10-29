using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Net;

/// <summary>
/// LAN TCP transport: length-prefixed frames, protocol version + modlist hash handshake,
/// one-host / N-client (default max 4). Default / fallback transport.
/// </summary>
public sealed class LanTcpTransport : ISessionLanTransport
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
    private int _nextPeerId; // Incremented to assign client ids starting at 1.
    private readonly object _sendLock = new();

    /// <summary>Max simultaneous clients. Default 4 (P0; was 1 in the Phase-1 toy lobby).</summary>
    public int MaxClients { get; set; } = 4;

    /// <summary>
    /// Host: expected ordered modlist hash. Client must send the same value in the handshake
    /// or the host rejects before JoinAck (hard gate).
    /// </summary>
    public string ExpectedModlistHash { get; set; } = string.Empty;

    /// <summary>Client: modlist hash advertised in the handshake.</summary>
    public string LocalModlistHash { get; set; } = string.Empty;

    /// <summary>Host sets this before <see cref="StartHostAsync"/>; embedded in JoinAck.</summary>
    public long HostSessionSeed { get; set; }

    /// <summary>Client: session seed delivered by JoinAck.</summary>
    public long? JoinedSessionSeed { get; private set; }

    /// <summary>Client: player id assigned by JoinAck.</summary>
    public int? AssignedPlayerId { get; private set; }

    /// <summary>Client: last join-reject reason, if the host refused the handshake.</summary>
    public JoinRejectReason? LastJoinRejectReason { get; private set; }

    /// <summary>Client: last join-reject message.</summary>
    public string? LastJoinRejectMessage { get; private set; }

    /// <summary>Highest protocol version observed on a peer handshake (host) or JoinAck (client).</summary>
    public byte RemoteProtocolVersion { get; private set; } = NetProtocol.Version;

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
        LastJoinRejectReason = null;
        LastJoinRejectMessage = null;

        var client = new TcpClient();
        await client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        var stream = client.GetStream();

        // Client → host handshake: magic + version + modlist hash (+ v4 trailer)
        var handshake = BuildHandshakePayload(
            NetProtocol.Version,
            LocalModlistHash,
            NetTransportKind.Tcp,
            HandshakeCodec.FlagPacingCapable);
        await FrameCodec.WriteFrameAsync(stream, NetMessageType.Handshake, handshake, cancellationToken)
            .ConfigureAwait(false);

        var (type, payload) = await FrameCodec.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
        if (type == NetMessageType.JoinReject)
        {
            ParseJoinReject(payload, out var reason, out var message);
            LastJoinRejectReason = reason;
            LastJoinRejectMessage = message;
            client.Dispose();
            throw new InvalidOperationException(
                $"Host rejected join: {reason} — {message}");
        }

        // JoinAck: version(1) + peerId(i32 LE) + sessionSeed(i64 LE)
        if (type != NetMessageType.JoinAck
            || !HandshakeCodec.TryParseJoinAck(payload, out var hostVersion, out var assignedId, out var seed))
        {
            client.Dispose();
            throw new InvalidOperationException("Host rejected handshake / join.");
        }

        if (!NetProtocol.IsCompatible(hostVersion))
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"Protocol version mismatch: host={hostVersion} local={NetProtocol.Version} (min={NetProtocol.MinSupportedVersion})");
        }

        RemoteProtocolVersion = hostVersion;
        AssignedPlayerId = assignedId;
        JoinedSessionSeed = seed;
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
        value is >= (byte)NetMessageType.Handshake and <= (byte)NetMessageType.JoinReject;

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
        _nextPeerId = 0;
        JoinedSessionSeed = null;
        AssignedPlayerId = null;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    public override string ToString() =>
        _mode == Mode.Idle ? "LanTcpTransport(idle)" : $"LanTcpTransport({_mode} {_endpoint} peers={_peers.Count})";

    public static byte[] BuildHandshakePayload(
        byte version,
        string modlistHash,
        NetTransportKind transportKind = NetTransportKind.Tcp,
        byte flags = 0) =>
        HandshakeCodec.BuildHandshakePayload(version, modlistHash, transportKind, flags);

    public static string ReadHandshakeModlistHash(ReadOnlySpan<byte> payload) =>
        HandshakeCodec.ReadModlistHash(payload);

    public static byte[] BuildJoinReject(JoinRejectReason reason, string message) =>
        HandshakeCodec.BuildJoinReject(reason, message);

    private static void ParseJoinReject(ReadOnlySpan<byte> payload, out JoinRejectReason reason, out string message) =>
        HandshakeCodec.ParseJoinReject(payload, out reason, out message);

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                if (_peers.Count >= MaxClients)
                {
                    await WriteRejectAndCloseAsync(client, JoinRejectReason.LobbyFull, $"Lobby full (max {MaxClients}).", cancellationToken)
                        .ConfigureAwait(false);
                    client = null;
                    continue;
                }

                var stream = client.GetStream();
                var (type, payload) = await FrameCodec.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (type != NetMessageType.Handshake || !HandshakeCodec.HasValidMagic(payload))
                {
                    client.Dispose();
                    client = null;
                    continue;
                }

                var remoteVersion = HandshakeCodec.ReadRemoteVersion(payload);
                if (!NetProtocol.IsCompatible(remoteVersion))
                {
                    await WriteRejectAndCloseAsync(
                            client,
                            JoinRejectReason.ProtocolMismatch,
                            $"Protocol mismatch: client={remoteVersion} host={NetProtocol.Version} (min={NetProtocol.MinSupportedVersion})",
                            cancellationToken)
                        .ConfigureAwait(false);
                    client = null;
                    continue;
                }

                RemoteProtocolVersion = remoteVersion;
                var clientHash = ReadHandshakeModlistHash(payload);
                if (!string.Equals(clientHash, ExpectedModlistHash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteRejectAndCloseAsync(
                            client,
                            JoinRejectReason.ModlistMismatch,
                            $"Modlist hash mismatch: client='{HandshakeCodec.Truncate(clientHash)}' host='{HandshakeCodec.Truncate(ExpectedModlistHash)}'",
                            cancellationToken)
                        .ConfigureAwait(false);
                    client = null;
                    continue;
                }

                var peerId = new NetPeerId(Interlocked.Increment(ref _nextPeerId));
                var ack = HandshakeCodec.BuildJoinAck(peerId.Value, HostSessionSeed);
                await FrameCodec.WriteFrameAsync(stream, NetMessageType.JoinAck, ack, cancellationToken)
                    .ConfigureAwait(false);

                var conn = new PeerConnection(peerId, client, stream);
                if (!_peers.TryAdd(peerId, conn))
                {
                    client.Dispose();
                    client = null;
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

    private static async Task WriteRejectAndCloseAsync(
        TcpClient client,
        JoinRejectReason reason,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            var stream = client.GetStream();
            await FrameCodec.WriteFrameAsync(stream, NetMessageType.JoinReject, BuildJoinReject(reason, message), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
        finally
        {
            client.Dispose();
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

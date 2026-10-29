using System.Collections.Concurrent;
using LiteNetLib;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Net;

/// <summary>
/// LAN reliable-UDP transport via LiteNetLib. Same join hard-gate semantics as
/// <see cref="LanTcpTransport"/>; application envelopes are <c>[NetMessageType][payload]</c>
/// without TCP length prefixes. Binds UDP on the game port (default 30555) — discovery
/// 30556 and ModSync 30557 stay separate.
/// </summary>
public sealed class LiteNetLibTransport : ISessionLanTransport
{
    private enum Mode
    {
        Idle,
        Host,
        Client,
    }

    private Mode _mode = Mode.Idle;
    private NetEndpoint? _endpoint;
    private EventBasedNetListener? _listener;
    private NetManager? _manager;
    private CancellationTokenSource? _cts;
    private Thread? _pollThread;
    private readonly ConcurrentDictionary<NetPeerId, NetPeer> _peers = new();
    private readonly ConcurrentDictionary<NetPeer, NetPeerId> _peerIds = new();
    private int _nextPeerId;
    private readonly object _sendLock = new();
    private TaskCompletionSource<bool>? _joinTcs;
    private NetPeer? _hostPeer;
    private int _connectAttempts;

    public int MaxClients { get; set; } = 4;
    public string ExpectedModlistHash { get; set; } = string.Empty;
    public string LocalModlistHash { get; set; } = string.Empty;
    public long HostSessionSeed { get; set; }
    public long? JoinedSessionSeed { get; private set; }
    public int? AssignedPlayerId { get; private set; }
    public JoinRejectReason? LastJoinRejectReason { get; private set; }
    public string? LastJoinRejectMessage { get; private set; }
    /// <summary>Highest protocol version observed on a peer handshake (host) or JoinAck (client).</summary>
    public byte RemoteProtocolVersion { get; private set; } = NetProtocol.Version;

    /// <summary>Client connect / reconnect backoff. Default retries transient UDP failures.</summary>
    public ReconnectPolicy ReconnectPolicy { get; set; } = ReconnectPolicy.Default;

    /// <summary>How many connect attempts the last <see cref="StartClientAsync"/> / reconnect used.</summary>
    public int LastConnectAttempts => _connectAttempts;

    public bool IsRunning => _mode != Mode.Idle;
    public NetEndpoint? Endpoint => _endpoint;
    public IReadOnlyCollection<NetPeerId> ConnectedPeers => _peers.Keys.ToArray();

    public event Action<NetPeerId, ReadOnlyMemory<byte>>? OnReceive;
    public event Action<NetPeerId>? OnPeerJoin;
    public event Action<NetPeerId>? OnPeerLeave;

    public Task StartHostAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (_mode != Mode.Idle)
            throw new InvalidOperationException("Transport already running.");

        _endpoint = endpoint;
        _mode = Mode.Host;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        WireListener();
        _manager = new NetManager(_listener!)
        {
            AutoRecycle = true,
            DisconnectTimeout = 15000,
        };

        if (!_manager.Start(endpoint.Port))
            throw new InvalidOperationException($"LiteNetLib failed to bind UDP port {endpoint.Port}.");

        StartPollThread();
        return Task.CompletedTask;
    }

    public async Task StartClientAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (_mode != Mode.Idle)
            throw new InvalidOperationException("Transport already running.");

        var policy = ReconnectPolicy ?? ReconnectPolicy.Default;
        var attempts = Math.Max(1, policy.MaxAttempts);
        Exception? lastError = null;
        _connectAttempts = 0;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var delay = policy.DelayBeforeAttempt(attempt);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

            _connectAttempts = attempt + 1;
            try
            {
                await ConnectOnceAsync(endpoint, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (InvalidOperationException) when (LastJoinRejectReason is not null)
            {
                // Hard gate (protocol / modlist / lobby full) — do not backoff-retry.
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                await StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        throw lastError ?? new TimeoutException($"LiteNetLib failed to join {endpoint} after {attempts} attempt(s).");
    }

    /// <summary>
    /// Client: tear down and rejoin the last endpoint with <see cref="ReconnectPolicy"/> backoff.
    /// Caller should follow with session-level JIP (<c>SessionRecovery.RequestRejoin</c>).
    /// </summary>
    public async Task TryReconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_endpoint is null)
            throw new InvalidOperationException("No prior endpoint to reconnect.");
        if (_mode == Mode.Host)
            throw new InvalidOperationException("Host cannot reconnect; clients rejoin the host.");

        var endpoint = _endpoint.Value;
        var hash = LocalModlistHash;
        var policy = ReconnectPolicy;
        await StopAsync(cancellationToken).ConfigureAwait(false);
        LocalModlistHash = hash;
        ReconnectPolicy = policy;
        await StartClientAsync(endpoint, cancellationToken).ConfigureAwait(false);
    }

    private async Task ConnectOnceAsync(NetEndpoint endpoint, CancellationToken cancellationToken)
    {
        _endpoint = endpoint;
        _mode = Mode.Client;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        LastJoinRejectReason = null;
        LastJoinRejectMessage = null;
        AssignedPlayerId = null;
        JoinedSessionSeed = null;
        _joinTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        WireListener();
        _manager = new NetManager(_listener!)
        {
            AutoRecycle = true,
            DisconnectTimeout = 15000,
        };

        if (!_manager.Start())
            throw new InvalidOperationException("LiteNetLib client failed to start.");

        StartPollThread();

        var peer = _manager.Connect(endpoint.Host, endpoint.Port, NetProtocol.LiteNetLibConnectKey);
        if (peer is null)
            throw new InvalidOperationException($"LiteNetLib connect to {endpoint} returned null.");

        using var reg = cancellationToken.Register(() => _joinTcs.TrySetCanceled(cancellationToken));
        try
        {
            await _joinTcs.Task.WaitAsync(TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"LiteNetLib join timed out contacting {endpoint}.");
        }

        if (LastJoinRejectReason is not null)
            throw new InvalidOperationException(
                $"Host rejected join: {LastJoinRejectReason} — {LastJoinRejectMessage}");
    }

    public void SendReliable(NetPeerId peer, ReadOnlyMemory<byte> payload)
    {
        if (!IsRunning)
            throw new InvalidOperationException("Transport is not running.");
        if (!_peers.TryGetValue(peer, out var netPeer))
            throw new InvalidOperationException($"Unknown peer {peer}.");
        WriteEnvelope(netPeer, payload);
    }

    public void BroadcastReliable(ReadOnlyMemory<byte> payload)
    {
        if (!IsRunning)
            throw new InvalidOperationException("Transport is not running.");
        foreach (var netPeer in _peers.Values)
            WriteEnvelope(netPeer, payload);
    }

    public void SendControl(NetPeerId peer, NetMessageType type, ReadOnlyMemory<byte> payload)
    {
        if (!_peers.TryGetValue(peer, out var netPeer))
            throw new InvalidOperationException($"Unknown peer {peer}.");
        WriteTyped(netPeer, type, payload);
    }

    public void BroadcastControl(NetMessageType type, ReadOnlyMemory<byte> payload)
    {
        foreach (var netPeer in _peers.Values)
            WriteTyped(netPeer, type, payload);
    }

    /// <summary>Host (or self): drop a connected peer (kick / leave). Mirrors <see cref="LanTcpTransport.DisconnectPeer"/>.</summary>
    public void DisconnectPeer(NetPeerId peer)
    {
        if (_mode == Mode.Idle)
            return;
        if (!_peers.TryGetValue(peer, out var netPeer))
            return;
        try
        {
            netPeer.Disconnect();
        }
        catch
        {
            // ignore
        }

        ForgetPeer(peer, raiseLeave: true);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_mode == Mode.Idle)
            return Task.CompletedTask;

        try { _cts?.Cancel(); } catch { /* ignore */ }

        try { _manager?.Stop(); } catch { /* ignore */ }
        _manager = null;
        _listener = null;

        if (_pollThread is not null)
        {
            try { _pollThread.Join(500); } catch { /* ignore */ }
            _pollThread = null;
        }

        foreach (var id in _peers.Keys.ToArray())
            ForgetPeer(id, raiseLeave: false);

        _peers.Clear();
        _peerIds.Clear();
        _cts?.Dispose();
        _cts = null;
        _mode = Mode.Idle;
        _endpoint = null;
        _nextPeerId = 0;
        _hostPeer = null;
        _joinTcs = null;
        JoinedSessionSeed = null;
        AssignedPlayerId = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    public override string ToString() =>
        _mode == Mode.Idle
            ? "LiteNetLibTransport(idle)"
            : $"LiteNetLibTransport({_mode} {_endpoint} peers={_peers.Count})";

    private void WireListener()
    {
        _listener = new EventBasedNetListener();
        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;
    }

    private void StartPollThread()
    {
        var cts = _cts ?? throw new InvalidOperationException("CTS missing.");
        _pollThread = new Thread(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    // LiteNetLib 1.3.x: process all queued events (default max = int.MaxValue).
                    _manager?.PollEvents(int.MaxValue);
                }
                catch
                {
                    // ignore poll errors during teardown
                }

                Thread.Sleep(1);
            }
        })
        {
            IsBackground = true,
            Name = "RimworldMP.LiteNetLib.Poll",
        };
        _pollThread.Start();
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (_mode != Mode.Host)
        {
            request.Reject();
            return;
        }

        if (_peers.Count >= MaxClients)
        {
            var reject = HandshakeCodec.BuildJoinReject(
                JoinRejectReason.LobbyFull,
                $"Lobby full (max {MaxClients}).");
            request.Reject(BuildTyped(NetMessageType.JoinReject, reject));
            return;
        }

        // Connect key is the sole ConnectionRequest gate; app handshake follows PeerConnected.
        if (request.AcceptIfKey(NetProtocol.LiteNetLibConnectKey) is null)
            return;
    }

    private void OnPeerConnected(NetPeer peer)
    {
        if (_mode == Mode.Client)
        {
            _hostPeer = peer;
            // Client → host handshake after UDP connect.
            var handshake = HandshakeCodec.BuildHandshakePayload(
                NetProtocol.Version,
                LocalModlistHash,
                NetTransportKind.LiteNetLib,
                HandshakeCodec.FlagPacingCapable);
            WriteTyped(peer, NetMessageType.Handshake, handshake);
            return;
        }

        // Host: wait for Handshake message before assigning peer id (see OnNetworkReceive).
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        if (_mode == Mode.Client && AssignedPlayerId is null && _joinTcs is not null)
        {
            if (info.AdditionalData is { AvailableBytes: > 0 })
            {
                var bytes = info.AdditionalData.GetRemainingBytes();
                if (bytes.Length >= 1 && bytes[0] == (byte)NetMessageType.JoinReject)
                {
                    HandshakeCodec.ParseJoinReject(bytes.AsSpan(1), out var reason, out var message);
                    LastJoinRejectReason = reason;
                    LastJoinRejectMessage = message;
                }
            }

            _joinTcs.TrySetException(new InvalidOperationException(
                LastJoinRejectReason is { } r
                    ? $"Host rejected join: {r} — {LastJoinRejectMessage}"
                    : "LiteNetLib disconnected before JoinAck."));
        }

        if (_peerIds.TryRemove(peer, out var id))
            ForgetPeer(id, raiseLeave: true);
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
        _ = channel;
        _ = method;
        var bytes = reader.GetRemainingBytes();
        if (bytes.Length < 1)
            return;

        var type = (NetMessageType)bytes[0];
        var payload = bytes.AsSpan(1);

        if (_mode == Mode.Host && type == NetMessageType.Handshake)
        {
            HandleHostHandshake(peer, payload);
            return;
        }

        if (_mode == Mode.Client && type == NetMessageType.JoinAck)
        {
            HandleClientJoinAck(peer, payload);
            return;
        }

        if (_mode == Mode.Client && type == NetMessageType.JoinReject)
        {
            HandshakeCodec.ParseJoinReject(payload, out var reason, out var message);
            LastJoinRejectReason = reason;
            LastJoinRejectMessage = message;
            _joinTcs?.TrySetException(new InvalidOperationException(
                $"Host rejected join: {reason} — {message}"));
            peer.Disconnect();
            return;
        }

        if (!_peerIds.TryGetValue(peer, out var id))
            return;

        OnReceive?.Invoke(id, bytes);
    }

    private void HandleHostHandshake(NetPeer peer, ReadOnlySpan<byte> payload)
    {
        if (!HandshakeCodec.HasValidMagic(payload))
        {
            peer.Disconnect();
            return;
        }

        var remoteVersion = HandshakeCodec.ReadRemoteVersion(payload);
        if (!NetProtocol.IsCompatible(remoteVersion))
        {
            var reject = HandshakeCodec.BuildJoinReject(
                JoinRejectReason.ProtocolMismatch,
                $"Protocol mismatch: client={remoteVersion} host={NetProtocol.Version} (min={NetProtocol.MinSupportedVersion})");
            WriteTyped(peer, NetMessageType.JoinReject, reject);
            peer.Disconnect(BuildTyped(NetMessageType.JoinReject, reject));
            return;
        }

        RemoteProtocolVersion = remoteVersion;
        var clientHash = HandshakeCodec.ReadModlistHash(payload);
        if (!string.Equals(clientHash, ExpectedModlistHash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            var reject = HandshakeCodec.BuildJoinReject(
                JoinRejectReason.ModlistMismatch,
                $"Modlist hash mismatch: client='{HandshakeCodec.Truncate(clientHash)}' host='{HandshakeCodec.Truncate(ExpectedModlistHash)}'");
            WriteTyped(peer, NetMessageType.JoinReject, reject);
            peer.Disconnect(BuildTyped(NetMessageType.JoinReject, reject));
            return;
        }

        if (_peers.Count >= MaxClients)
        {
            var reject = HandshakeCodec.BuildJoinReject(
                JoinRejectReason.LobbyFull,
                $"Lobby full (max {MaxClients}).");
            WriteTyped(peer, NetMessageType.JoinReject, reject);
            peer.Disconnect(BuildTyped(NetMessageType.JoinReject, reject));
            return;
        }

        var peerId = new NetPeerId(Interlocked.Increment(ref _nextPeerId));
        if (!_peers.TryAdd(peerId, peer) || !_peerIds.TryAdd(peer, peerId))
        {
            peer.Disconnect();
            return;
        }

        var ack = HandshakeCodec.BuildJoinAck(peerId.Value, HostSessionSeed);
        WriteTyped(peer, NetMessageType.JoinAck, ack);
        OnPeerJoin?.Invoke(peerId);
    }

    private void HandleClientJoinAck(NetPeer peer, ReadOnlySpan<byte> payload)
    {
        if (!HandshakeCodec.TryParseJoinAck(payload, out var hostVersion, out var assignedId, out var seed))
        {
            _joinTcs?.TrySetException(new InvalidOperationException("Host rejected handshake / join."));
            return;
        }

        if (!NetProtocol.IsCompatible(hostVersion))
        {
            _joinTcs?.TrySetException(new InvalidOperationException(
                $"Protocol version mismatch: host={hostVersion} local={NetProtocol.Version} (min={NetProtocol.MinSupportedVersion})"));
            return;
        }

        RemoteProtocolVersion = hostVersion;
        AssignedPlayerId = assignedId;
        JoinedSessionSeed = seed;
        var id = new NetPeerId(assignedId);
        _peers[NetPeerId.Host] = peer;
        _peerIds[peer] = NetPeerId.Host;
        OnPeerJoin?.Invoke(NetPeerId.Host);
        _joinTcs?.TrySetResult(true);
    }

    private void WriteEnvelope(NetPeer peer, ReadOnlyMemory<byte> payload)
    {
        if (payload.Length >= 1 && IsKnownType(payload.Span[0]))
            WriteTyped(peer, (NetMessageType)payload.Span[0], payload[1..]);
        else
            WriteTyped(peer, NetMessageType.CommandBatch, payload);
    }

    private void WriteTyped(NetPeer peer, NetMessageType type, ReadOnlyMemory<byte> payload)
    {
        lock (_sendLock)
        {
            try
            {
                peer.Send(BuildTyped(type, payload.Span), DeliveryMethod.ReliableOrdered);
            }
            catch
            {
                if (_peerIds.TryGetValue(peer, out var id))
                    ForgetPeer(id, raiseLeave: true);
            }
        }
    }

    private static byte[] BuildTyped(NetMessageType type, ReadOnlySpan<byte> payload)
    {
        var buf = new byte[1 + payload.Length];
        buf[0] = (byte)type;
        payload.CopyTo(buf.AsSpan(1));
        return buf;
    }

    private static bool IsKnownType(byte value) =>
        value is >= (byte)NetMessageType.Handshake and <= (byte)NetMessageType.KickNotice;

    private void ForgetPeer(NetPeerId id, bool raiseLeave)
    {
        if (_peers.TryRemove(id, out var peer))
            _peerIds.TryRemove(peer, out _);
        if (raiseLeave)
            OnPeerLeave?.Invoke(id);
    }
}

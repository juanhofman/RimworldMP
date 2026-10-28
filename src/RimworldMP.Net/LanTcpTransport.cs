namespace RimworldMP.Net;

/// <summary>
/// LAN-first TCP placeholder. Phase 0: tracks lifecycle state only — no sockets yet.
/// Replace internals with real TcpListener / LiteNetLib without changing <see cref="INetTransport"/>.
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

    public bool IsRunning => _mode != Mode.Idle;

    public event Action<NetPeerId, ReadOnlyMemory<byte>>? OnReceive;
    public event Action<NetPeerId>? OnPeerJoin;
    public event Action<NetPeerId>? OnPeerLeave;

    // Keep event fields "used" until Phase 1 wires the socket loop.
    private void RaisePeerJoin(NetPeerId peer) => OnPeerJoin?.Invoke(peer);
    private void RaisePeerLeave(NetPeerId peer) => OnPeerLeave?.Invoke(peer);
    private void RaiseReceive(NetPeerId peer, ReadOnlyMemory<byte> payload) => OnReceive?.Invoke(peer, payload);

    public Task StartHostAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (_mode != Mode.Idle)
            throw new InvalidOperationException("Transport already running.");

        _endpoint = endpoint;
        _mode = Mode.Host;
        // TODO Phase 1: bind TcpListener, accept peers, length-prefixed frames.
        return Task.CompletedTask;
    }

    public Task StartClientAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (_mode != Mode.Idle)
            throw new InvalidOperationException("Transport already running.");

        _endpoint = endpoint;
        _mode = Mode.Client;
        // TODO Phase 1: connect TcpClient, handshake protocol version.
        return Task.CompletedTask;
    }

    public void SendReliable(NetPeerId peer, ReadOnlyMemory<byte> payload)
    {
        if (!IsRunning)
            throw new InvalidOperationException("Transport is not running.");
        // TODO Phase 1: enqueue reliable send to peer.
        _ = peer;
        _ = payload;
        _ = nameof(RaiseReceive);
    }

    public void BroadcastReliable(ReadOnlyMemory<byte> payload)
    {
        if (!IsRunning)
            throw new InvalidOperationException("Transport is not running.");
        // TODO Phase 1: send to all peers.
        _ = payload;
        _ = nameof(RaisePeerJoin);
        _ = nameof(RaisePeerLeave);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _mode = Mode.Idle;
        _endpoint = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    public override string ToString() =>
        _mode == Mode.Idle ? "LanTcpTransport(idle)" : $"LanTcpTransport({_mode} {_endpoint})";
}

using System.Collections.Concurrent;

namespace RimworldMP.Net;

/// <summary>
/// In-process transport for unit tests / determinism harness. Pairs two instances via <see cref="Pair"/>.
/// </summary>
public sealed class LoopbackTransport : INetTransport
{
    private readonly ConcurrentQueue<(NetPeerId Peer, byte[] Payload)> _inbox = new();
    private LoopbackTransport? _remote;
    private NetPeerId _localId = NetPeerId.Host;
    private NetPeerId _remoteId = new(1);
    private bool _running;
    private int _pumpScheduled;

    public bool IsRunning => _running;

    public event Action<NetPeerId, ReadOnlyMemory<byte>>? OnReceive;
    public event Action<NetPeerId>? OnPeerJoin;
    public event Action<NetPeerId>? OnPeerLeave;

    public static void Pair(LoopbackTransport host, LoopbackTransport client)
    {
        host._remote = client;
        client._remote = host;
        host._localId = NetPeerId.Host;
        host._remoteId = new NetPeerId(1);
        client._localId = new NetPeerId(1);
        client._remoteId = NetPeerId.Host;
    }

    public Task StartHostAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        _ = endpoint;
        if (_running)
            throw new InvalidOperationException("Transport already running.");
        _running = true;
        if (_remote is { _running: true })
        {
            OnPeerJoin?.Invoke(_remoteId);
            _remote.OnPeerJoin?.Invoke(_remote._remoteId);
        }
        return Task.CompletedTask;
    }

    public Task StartClientAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        _ = endpoint;
        if (_running)
            throw new InvalidOperationException("Transport already running.");
        _running = true;
        if (_remote is { _running: true })
        {
            OnPeerJoin?.Invoke(_remoteId);
            _remote.OnPeerJoin?.Invoke(_remote._remoteId);
        }
        return Task.CompletedTask;
    }

    public void SendReliable(NetPeerId peer, ReadOnlyMemory<byte> payload)
    {
        if (!_running)
            throw new InvalidOperationException("Transport is not running.");
        if (_remote is null)
            throw new InvalidOperationException("Loopback transport is not paired.");
        _ = peer;
        _remote.Enqueue(_localId, payload);
    }

    public void BroadcastReliable(ReadOnlyMemory<byte> payload)
    {
        SendReliable(_remoteId, payload);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_running)
            return Task.CompletedTask;
        _running = false;
        if (_remote is not null)
            OnPeerLeave?.Invoke(_remoteId);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private void Enqueue(NetPeerId from, ReadOnlyMemory<byte> payload)
    {
        _inbox.Enqueue((from, payload.ToArray()));
        SchedulePump();
    }

    private void SchedulePump()
    {
        if (Interlocked.CompareExchange(ref _pumpScheduled, 1, 0) != 0)
            return;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                while (_inbox.TryDequeue(out var item))
                    OnReceive?.Invoke(item.Peer, item.Payload);
            }
            finally
            {
                Interlocked.Exchange(ref _pumpScheduled, 0);
                if (!_inbox.IsEmpty)
                    SchedulePump();
            }
        });
    }
}

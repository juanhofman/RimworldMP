namespace RimworldMP.Net;

/// <summary>
/// Replaceable networking facade. Implementations: loopback (tests), LAN TCP, LiteNetLib, etc.
/// </summary>
public interface INetTransport : IAsyncDisposable
{
    bool IsRunning { get; }

    event Action<NetPeerId, ReadOnlyMemory<byte>>? OnReceive;
    event Action<NetPeerId>? OnPeerJoin;
    event Action<NetPeerId>? OnPeerLeave;

    Task StartHostAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default);
    Task StartClientAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default);

    void SendReliable(NetPeerId peer, ReadOnlyMemory<byte> payload);
    void BroadcastReliable(ReadOnlyMemory<byte> payload);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public readonly record struct NetPeerId(int Value)
{
    public static readonly NetPeerId Host = new(0);
    public override string ToString() => Value.ToString();
}

public readonly record struct NetEndpoint(string Host, int Port)
{
    public override string ToString() => $"{Host}:{Port}";
}

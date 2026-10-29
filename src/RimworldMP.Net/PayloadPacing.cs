using RimworldMP.Net.Protocol;

namespace RimworldMP.Net;

/// <summary>
/// JIP / large-payload pacing helpers. Chunk sizing stays compatible with protocol v3
/// receivers (default <see cref="NetProtocol.JoinPointChunkBytes"/>); v4+ peers may
/// negotiate a smaller LiteNetLib-friendly size via the join-point header trailer.
/// </summary>
public static class PayloadPacing
{
    /// <summary>Smaller UDP-friendly chunk for LiteNetLib when both peers are v4+.</summary>
    public static int LiteNetLibChunkBytes => NetProtocol.LiteNetLibJoinPointChunkBytes;

    public static int ResolveChunkBytes(NetTransportKind kind, byte remoteVersion = NetProtocol.Version)
    {
        if (kind == NetTransportKind.LiteNetLib
            && remoteVersion >= 4
            && NetProtocol.Version >= 4)
            return NetProtocol.LiteNetLibJoinPointChunkBytes;
        return NetProtocol.JoinPointChunkBytes;
    }

    public static int ResolveChunkBytes(INetTransport transport, byte remoteVersion = NetProtocol.Version)
    {
        var kind = transport switch
        {
            LiteNetLibTransport => NetTransportKind.LiteNetLib,
            LanTcpTransport => NetTransportKind.Tcp,
            _ => NetTransportKind.Tcp,
        };
        return ResolveChunkBytes(kind, remoteVersion);
    }

    /// <summary>
    /// Delay between reliable chunk sends. LiteNetLib benefits from pacing so the
    /// reliable window is not flooded by a multi-megabyte JIP blob.
    /// </summary>
    public static TimeSpan InterChunkDelay(NetTransportKind kind) =>
        kind == NetTransportKind.LiteNetLib
            ? TimeSpan.FromMilliseconds(NetProtocol.LiteNetLibInterChunkDelayMs)
            : TimeSpan.Zero;

    public static TimeSpan InterChunkDelay(INetTransport transport) =>
        transport is LiteNetLibTransport
            ? InterChunkDelay(NetTransportKind.LiteNetLib)
            : TimeSpan.Zero;

    /// <summary>
    /// Send envelopes one-by-one with optional inter-chunk delay.
    /// Synchronous; safe for host tick threads that already block on JIP.
    /// </summary>
    public static void SendPaced(
        INetTransport transport,
        NetPeerId peer,
        IEnumerable<ReadOnlyMemory<byte>> envelopes,
        TimeSpan? delayOverride = null)
    {
        var delay = delayOverride ?? InterChunkDelay(transport);
        foreach (var envelope in envelopes)
        {
            transport.SendReliable(peer, envelope);
            if (delay > TimeSpan.Zero)
                Thread.Sleep(delay);
        }
    }
}

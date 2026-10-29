using RimworldMP.Core.Join;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Session;

/// <summary>Soft recovery: freeze → request fresh join point from host; peer-leave / rejoin polish.</summary>
public static class SessionRecovery
{
    public static void RequestRejoin(MpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsHost)
            throw new InvalidOperationException("Host cannot rejoin itself; rebuild join point for clients instead.");
        session.Desync.Reset();
        session.Chat.AppendSystem(
            SystemNoticeKind.Recovery,
            SessionMessages.Get("RMP.RecoveryRequested"),
            session.LocalPlayerId,
            session.Roster.GetName(session.LocalPlayerId, isHostSeat: false),
            session.Tick);
        session.RequestJoinPoint();
    }

    public static JoinPoint? BuildHostRecoveryPoint(MpSession session, byte[]? saveBlob = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsHost)
            throw new InvalidOperationException("Only host builds recovery join points.");
        var blob = saveBlob
                   ?? session.JoinPointSource?.TryCreateJoinPoint(session.Tick + 1, session.SessionSeed, session.Tick)?.SaveBlob
                   ?? Array.Empty<byte>();
        return new JoinPoint(session.Tick + 1, session.SessionSeed, session.Tick, session.ModlistHash, blob);
    }

    /// <summary>Host: push a recovery join point to one peer (after disconnect/rejoin or desync).</summary>
    public static bool TryHostResyncPeer(MpSession session, NetPeerId peer, byte[]? saveBlob = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsHost || !session.IsActive)
            return false;

        var jp = BuildHostRecoveryPoint(session, saveBlob);
        session.HostSendJoinPoint(peer, jp);
        session.Chat.AppendSystem(
            SystemNoticeKind.Recovery,
            SessionMessages.Format("RMP.RecoverySent", peer.Value),
            peer.Value,
            session.Roster.GetName(peer.Value),
            session.Tick);
        return true;
    }

    /// <summary>
    /// Clear ready for a departed peer and optionally pause. Call from session peer-leave path.
    /// </summary>
    public static void HandlePeerDeparted(MpSession session, int playerId, bool kicked)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Ready.RemovePeer(playerId);
        var name = session.Roster.GetName(playerId, isHostSeat: playerId == 0);
        session.Roster.Remove(playerId);
        var kind = kicked ? SystemNoticeKind.PeerKicked : SystemNoticeKind.PeerLeft;
        var text = kicked
            ? SessionMessages.Format("RMP.PeerKicked", name)
            : SessionMessages.Format("RMP.PeerLeft", name);

        // Host broadcasts (includes local append). Clients only append locally.
        if (session.IsHost)
            session.BroadcastSystemNotice(kind, text);
        else
            session.Chat.AppendSystem(kind, text, playerId, name, session.Tick);
    }
}

using RimworldMP.Core.Join;

namespace RimworldMP.Core.Session;

/// <summary>Soft recovery: freeze → request fresh join point from host.</summary>
public static class SessionRecovery
{
    public static void RequestRejoin(MpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsHost)
            throw new InvalidOperationException("Host cannot rejoin itself; rebuild join point for clients instead.");
        session.Desync.Reset();
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
}

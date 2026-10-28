using RimworldMP.Core.Commands;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Session;

/// <summary>
/// Process-wide session accessor used by Harmony patches and the public API.
/// </summary>
public static class SessionFacade
{
    private static MpSession? _current;
    private static readonly object Gate = new();

    public static MpSession? Current
    {
        get { lock (Gate) return _current; }
    }

    public static bool IsInSession
    {
        get
        {
            var s = Current;
            return s is { IsActive: true };
        }
    }

    public static bool IsHost => Current?.IsHost == true;

    public static int LocalPlayerId => Current?.LocalPlayerId ?? -1;

    public static MpSession Attach(MpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (Gate)
            _current = session;
        return session;
    }

    public static void Clear()
    {
        lock (Gate)
            _current = null;
    }

    public static async Task<MpSession> HostLobbyAsync(
        INetTransport? transport = null,
        int port = NetProtocol.DefaultPort,
        long? sessionSeed = null,
        CancellationToken cancellationToken = default)
    {
        var session = new MpSession(transport ?? new LanTcpTransport(), isHost: true, sessionSeed);
        Attach(session);
        await session.StartAsync(new NetEndpoint("0.0.0.0", port), cancellationToken).ConfigureAwait(false);
        return session;
    }

    public static async Task<MpSession> JoinLobbyAsync(
        string host,
        int port = NetProtocol.DefaultPort,
        INetTransport? transport = null,
        long? sessionSeed = null,
        CancellationToken cancellationToken = default)
    {
        var session = new MpSession(transport ?? new LanTcpTransport(), isHost: false, sessionSeed);
        Attach(session);
        await session.StartAsync(new NetEndpoint(host, port), cancellationToken).ConfigureAwait(false);
        return session;
    }

    public static void Submit(CommandOpcode opcode, byte[]? payload = null)
    {
        var session = Current ?? throw new InvalidOperationException("No active RimworldMP session.");
        session.SubmitCommand(opcode, payload);
    }
}

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
        int port = -1,
        long? sessionSeed = null,
        string? modlistHash = null,
        int? maxClients = null,
        CancellationToken cancellationToken = default)
    {
        if (port < 0)
            port = MpConfig.DefaultPort;
        var hash = modlistHash ?? MpConfig.ModlistHash;
        var lan = transport as LanTcpTransport;
        if (transport is null)
        {
            lan = new LanTcpTransport
            {
                MaxClients = Math.Max(1, maxClients ?? MpConfig.MaxClients),
                ExpectedModlistHash = hash,
            };
            transport = lan;
        }
        else if (lan is not null)
        {
            if (maxClients is int mc)
                lan.MaxClients = Math.Max(1, mc);
            else if (lan.MaxClients < 1)
                lan.MaxClients = MpConfig.MaxClients;
            lan.ExpectedModlistHash = hash;
        }

        var session = new MpSession(transport, isHost: true, sessionSeed);
        session.SetModlistHash(hash);
        if (MpConfig.ReadyRequired)
            session.Ready.Required = true;
        Attach(session);
        await session.StartAsync(new NetEndpoint("0.0.0.0", port), cancellationToken).ConfigureAwait(false);
        return session;
    }

    public static async Task<MpSession> JoinLobbyAsync(
        string host,
        int port = -1,
        INetTransport? transport = null,
        long? sessionSeed = null,
        string? modlistHash = null,
        CancellationToken cancellationToken = default)
    {
        if (port < 0)
            port = MpConfig.DefaultPort;
        var hash = modlistHash ?? MpConfig.ModlistHash;
        var lan = transport as LanTcpTransport;
        if (transport is null)
        {
            lan = new LanTcpTransport { LocalModlistHash = hash };
            transport = lan;
        }
        else if (lan is not null)
        {
            lan.LocalModlistHash = hash;
        }

        var session = new MpSession(transport, isHost: false, sessionSeed);
        session.SetModlistHash(hash);
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

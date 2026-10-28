using RimworldMP.CommandApply;
using RimworldMP.Core.Session;
using RimworldMP.Net.Protocol;
using RimworldMP.API;

#if !RIMWORLDMP_NO_GAME_REFS
using Verse;
#endif

namespace RimworldMP.Lobby;

/// <summary>
/// Host / join entry points for the Phase 1 lobby. Invoked from DevMode debug actions when game refs exist,
/// or directly from tests / harnesses without Verse.
/// </summary>
public static class MpLobbyActions
{
    public static async Task HostAsync(int port = NetProtocol.DefaultPort, long? seed = null)
    {
        await StopAsync().ConfigureAwait(false);
        var session = await SessionFacade.HostLobbyAsync(port: port, sessionSeed: seed).ConfigureAwait(false);
        WireSession(session);
#if !RIMWORLDMP_NO_GAME_REFS
        Log.Message($"[RimworldMP] Hosting lobby on port {port}, seed={session.SessionSeed}");
#endif
    }

    public static async Task JoinAsync(string host, int port = NetProtocol.DefaultPort, long? seed = null)
    {
        await StopAsync().ConfigureAwait(false);
        // Seed is optional: JoinAck delivers the host session seed over LAN TCP.
        var session = await SessionFacade.JoinLobbyAsync(host, port, sessionSeed: seed).ConfigureAwait(false);
        WireSession(session);
#if !RIMWORLDMP_NO_GAME_REFS
        Log.Message($"[RimworldMP] Joined lobby at {host}:{port}, seed={session.SessionSeed}, player={session.LocalPlayerId}");
#endif
    }

    public static async Task StopAsync()
    {
        var current = SessionFacade.Current;
        if (current is null)
            return;
        await current.StopAsync().ConfigureAwait(false);
        await current.DisposeAsync().ConfigureAwait(false);
        SessionFacade.Clear();
    }

    private static void WireSession(MpSession session)
    {
        session.OnBatchApply += CommandApplicator.Apply;
        session.OnDesync += info =>
        {
#if !RIMWORLDMP_NO_GAME_REFS
            Log.Error($"[RimworldMP] DESYNC at tick {info.Tick}. Dump: {info.LogPath}");
            Find.TickManager?.Pause();
#endif
            MP.DesyncRaise?.Invoke();
        };

        MP.Bind(
            () => SessionFacade.IsInSession,
            () => SessionFacade.IsHost,
            () => SessionFacade.LocalPlayerId,
            (opcode, payload) => SessionFacade.Submit((Core.Commands.CommandOpcode)opcode, payload),
            () => { });
    }
}

using RimworldMP.API;
using RimworldMP.CommandApply;
using RimworldMP.Core.Join;
using RimworldMP.Core.Lobby;
using RimworldMP.Core.Session;
using RimworldMP.Join;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;
using RimworldMP.UI;

#if !RIMWORLDMP_NO_GAME_REFS
using Verse;
#endif

namespace RimworldMP.Lobby;

/// <summary>Host / join / JIP / launcher-handoff entry points for UI, DevMode, and harnesses.</summary>
public static class MpLobbyActions
{
    public static async Task HostAsync(
        int port = -1,
        long? seed = null,
        IJoinPointSource? joinPointSource = null,
        string? modlistHash = null,
        int? maxClients = null)
    {
        await StopAsync().ConfigureAwait(false);
        if (port < 0)
            port = MpConfig.DefaultPort;

        EnsureGameLoad();
        var session = await SessionFacade.HostLobbyAsync(
            port: port,
            sessionSeed: seed,
            modlistHash: modlistHash,
            maxClients: maxClients).ConfigureAwait(false);
        session.JoinPointSource = joinPointSource;
        session.SaveLoad ??= GameLoad.Current;
        WireSession(session);
#if !RIMWORLDMP_NO_GAME_REFS
        Log.Message(
            $"[RimworldMP] Hosting lobby on port {port}, seed={session.SessionSeed}, maxClients={maxClients ?? MpConfig.MaxClients}, modlist={session.ModlistHash}");
#endif
    }

    public static async Task JoinAsync(
        string host,
        int port = -1,
        long? seed = null,
        string? modlistHash = null)
    {
        await StopAsync().ConfigureAwait(false);
        if (port < 0)
            port = MpConfig.DefaultPort;

        EnsureGameLoad();
        var session = await SessionFacade.JoinLobbyAsync(
            host,
            port,
            sessionSeed: seed,
            modlistHash: modlistHash).ConfigureAwait(false);
        session.SaveLoad ??= GameLoad.Current;
        WireSession(session);
#if !RIMWORLDMP_NO_GAME_REFS
        Log.Message(
            $"[RimworldMP] Joined lobby at {host}:{port}, seed={session.SessionSeed}, player={session.LocalPlayerId}, modlist={session.ModlistHash}");
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

    /// <summary>
    /// Consume <c>launcher-handoff.json</c> if present: apply config and optionally auto host/join.
    /// Returns true when a handoff file was found (whether or not auto-start ran).
    /// Updates <see cref="HandoffUiTracker"/> for lobby status copy.
    /// </summary>
    public static async Task<bool> TryConsumeLauncherHandoffAsync(string? path = null)
    {
        path ??= LauncherHandoff.DefaultPath;
        LauncherHandoffDocument? doc;
        try
        {
            doc = LauncherHandoff.TryRead(path);
        }
        catch (Exception ex)
        {
            HandoffUiTracker.RecordFailed(path, ex.Message);
#if !RIMWORLDMP_NO_GAME_REFS
            Log.Error($"[RimworldMP] launcher-handoff read failed: {ex.Message}");
#else
            _ = ex;
#endif
            return false;
        }

        if (doc is null)
        {
            HandoffUiTracker.RecordMissing(path);
            return false;
        }

        if (!string.IsNullOrEmpty(doc.ModlistHash))
            MpConfig.ModlistHash = doc.ModlistHash!;
        if (doc.MaxClients is int mc && mc > 0)
            MpConfig.MaxClients = mc;
        if (doc.Port > 0)
            MpConfig.DefaultPort = doc.Port;
        if (NetTransportFactory.TryParse(doc.Transport, out var transportKind))
            MpConfig.TransportKind = transportKind;

        LauncherHandoff.Acknowledge(path);

        var autoStart = doc.AutoStartLobby;
        if (!autoStart)
        {
            HandoffUiTracker.RecordConsumed(path, doc, autoStarted: false);
            return true;
        }

        if (doc.IsHostRole)
        {
            await HostAsync(port: doc.Port, modlistHash: doc.ModlistHash, maxClients: doc.MaxClients)
                .ConfigureAwait(false);
            HandoffUiTracker.RecordConsumed(path, doc, autoStarted: true);
        }
        else if (doc.IsClientRole)
        {
            var host = string.IsNullOrWhiteSpace(doc.Host) ? "127.0.0.1" : doc.Host!;
            await JoinAsync(host, port: doc.Port, modlistHash: doc.ModlistHash).ConfigureAwait(false);
            HandoffUiTracker.RecordConsumed(path, doc, autoStarted: true);
        }
        else
        {
            HandoffUiTracker.RecordConsumed(path, doc, autoStarted: false);
        }

        return true;
    }

    private static void EnsureGameLoad()
    {
        GameLoad.Current ??= GameLoadHooks.TryCreateDefault();
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
        session.OnJoinPointReceived += jp =>
        {
            // Load already ran inside MpSession.ApplyJoinPoint via ISaveLoad (fails loud if missing).
#if !RIMWORLDMP_NO_GAME_REFS
            Log.Message(
                $"[RimworldMP] JIP applied watermark={jp.CommandWatermark} blob={jp.SaveBlob.Length}B modlist={jp.ModlistHash}");
#else
            _ = jp;
#endif
        };

        MP.Bind(
            () => SessionFacade.IsInSession,
            () => SessionFacade.IsHost,
            () => SessionFacade.LocalPlayerId,
            (opcode, payload) => SessionFacade.Submit((Core.Commands.CommandOpcode)opcode, payload),
            () => { },
            tick: () => SessionFacade.Current?.Tick ?? -1);
    }
}

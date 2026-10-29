using System.Reflection;
using HarmonyLib;
using RimworldMP.API;
using RimworldMP.Core.Session;
using RimworldMP.Join;
using RimworldMP.Lobby;
using RimworldMP.Net.Protocol;
using RimworldMP.UI;

#if !RIMWORLDMP_NO_GAME_REFS
using Verse;
#endif

namespace RimworldMP;

/// <summary>
/// Mod entry. With game refs: subclasses Verse.Mod and applies Harmony. Without: static bootstrap for CI.
/// </summary>
#if RIMWORLDMP_NO_GAME_REFS
public sealed class RimworldMPMod
{
    public const string HarmonyId = "RimworldMP.Core";

    public static MpSession? Session => SessionFacade.Current;

    static RimworldMPMod()
    {
        _ = new Harmony(HarmonyId);
        // Session is created explicitly via MpLobbyActions in harness / tests.
        MP.Bind(
            () => SessionFacade.IsInSession,
            () => SessionFacade.IsHost,
            () => SessionFacade.LocalPlayerId,
            (opcode, payload) =>
            {
                if (SessionFacade.IsInSession)
                    SessionFacade.Submit((Core.Commands.CommandOpcode)opcode, payload);
            },
            () => { },
            tick: () => SessionFacade.Current?.Tick ?? -1);
    }
}
#else
public sealed class RimworldMPMod : Mod
{
    public const string HarmonyId = "RimworldMP.Core";

    public static RimworldMPMod? Instance { get; private set; }
    public static MpSession? Session => SessionFacade.Current;

    public RimworldMPMod(ModContentPack content) : base(content)
    {
        Instance = this;
        var harmony = new Harmony(HarmonyId);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        Core.Join.GameLoad.Current ??= GameLoadHooks.TryCreateDefault();

        MP.Bind(
            () => SessionFacade.IsInSession,
            () => SessionFacade.IsHost,
            () => SessionFacade.LocalPlayerId,
            (opcode, payload) =>
            {
                if (SessionFacade.IsInSession)
                    SessionFacade.Submit((Core.Commands.CommandOpcode)opcode, payload);
            },
            () => { },
            tick: () => SessionFacade.Current?.Tick ?? -1);

        // Launcher handoff: auto host/join without DevMode when the external launcher wrote JSON.
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            _ = MpLobbyActions.TryConsumeLauncherHandoffAsync();
        });

        Log.Message(
            $"[RimworldMP] Release lobby UI loaded (protocol v{NetProtocol.Version}, maxClients default {MpConfig.DefaultMaxClients}). " +
            "Lobby: corner button / F10 / launcher-handoff.json (DevMode actions remain).");
    }
}

/// <summary>DevMode debug actions — fallback beside the non-DevMode lobby UI.</summary>
public static class RimworldMPDebugActions
{
    [DebugAction("RimworldMP", "Open lobby UI", allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.Playing)]
    private static void OpenLobbyUi() => LobbyUi.Open();

    [DebugAction("RimworldMP", "Host lobby (LAN)", allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.Playing)]
    private static void HostLobby()
    {
        _ = MpLobbyActions.HostAsync();
        Messages.Message("RimworldMP: hosting lobby on port " + MpConfig.DefaultPort, MessageTypeDefOf.TaskCompletion, false);
    }

    [DebugAction("RimworldMP", "Join lobby localhost", allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.Playing)]
    private static void JoinLobbyLocal()
    {
        _ = MpLobbyActions.JoinAsync("127.0.0.1");
        Messages.Message("RimworldMP: joining 127.0.0.1", MessageTypeDefOf.TaskCompletion, false);
    }

    [DebugAction("RimworldMP", "Stop lobby / session", allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.Playing)]
    private static void StopLobby()
    {
        _ = MpLobbyActions.StopAsync();
        Messages.Message("RimworldMP: session stopped", MessageTypeDefOf.TaskCompletion, false);
    }

    [DebugAction("RimworldMP", "Consume launcher handoff", allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.Playing)]
    private static void ConsumeHandoff()
    {
        _ = MpLobbyActions.TryConsumeLauncherHandoffAsync();
        Messages.Message("RimworldMP: checked launcher-handoff.json", MessageTypeDefOf.TaskCompletion, false);
    }
}
#endif

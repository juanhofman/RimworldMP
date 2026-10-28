using System.Reflection;
using HarmonyLib;
using RimworldMP.API;
using RimworldMP.Core.Session;
using RimworldMP.Lobby;
using RimworldMP.Net.Protocol;

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
            () => { });
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

        MP.Bind(
            () => SessionFacade.IsInSession,
            () => SessionFacade.IsHost,
            () => SessionFacade.LocalPlayerId,
            (opcode, payload) =>
            {
                if (SessionFacade.IsInSession)
                    SessionFacade.Submit((Core.Commands.CommandOpcode)opcode, payload);
            },
            () => { });

        Log.Message("[RimworldMP] Phase 1 lockstep loaded. Use DevMode: 'RimworldMP: Host lobby' / 'Join lobby' (LAN TCP).");
    }
}

/// <summary>DevMode debug actions for host/join smoke tests.</summary>
public static class RimworldMPDebugActions
{
    [DebugAction("RimworldMP", "Host lobby (LAN)", allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.Playing)]
    private static void HostLobby()
    {
        _ = MpLobbyActions.HostAsync();
        Messages.Message("RimworldMP: hosting lobby on port " + NetProtocol.DefaultPort, MessageTypeDefOf.TaskCompletion, false);
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
}
#endif

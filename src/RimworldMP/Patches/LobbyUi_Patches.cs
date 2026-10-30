#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.Core.Session;
using RimworldMP.UI;
using UnityEngine;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Non-DevMode entry: F10 toggles lobby; brand chip above main tabs; desync freeze overlay;
/// paused-session command pump (unpause cannot wait on DoSingleTick).
/// Avoids fragile MainMenuDrawer / PauseMenu signature patches — re-check under RIMWORLD_DIR.
/// </summary>
/// <remarks>
/// UNVERIFIED (RW 1.7): UIRoot.UIRootOnGUI + UnityEngine.Event / Widgets under Waylith stubs.
/// Prefer this stable hook over inventing MainMenuDrawer Prefixes.
/// </remarks>
[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
internal static class LobbyUi_Patches
{
    private static void Postfix()
    {
        // While paused, vanilla never calls DoSingleTick — flush staged unpause/speed here.
        PumpPausedSessionCommands();

        if (Event.current.type == EventType.KeyDown
            && Event.current.keyCode == KeyCode.F10
            && !Event.current.control
            && !Event.current.alt
            && !Event.current.shift)
        {
            LobbyUi.Open();
            Event.current.Use();
        }

        // Desync veil first so it sits under the corner chip / open windows as needed.
        DesyncFreezeOverlay.DrawIfNeeded();

        // Brand chip sits above the Architect / main-tab strip — never overlapping it.
        var stack = Find.WindowStack;
        var lobbyOpen = stack?.WindowOfType<Dialog_RimworldMPLobby>() is not null;
        if (MpImmediateGui.DrawHudChip(MpImmediateGui.HudChipRect(), MpUiPalette.BrandName, active: lobbyOpen))
            LobbyUi.Open();
    }

    private static void PumpPausedSessionCommands()
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return;

        var tm = Find.TickManager;
        if (tm is null || !tm.Paused)
            return;

        // Only when something is waiting: empty emit-every-frame would advance ticks while paused.
        if (session.IsHost && !session.HasStagedCommands && session.TickGate.PendingCount == 0)
            return;
        // Clients: only pump when a batch already arrived for the next tick.
        if (!session.IsHost && session.TickGate.PendingCount == 0)
            return;

        session.PumpPausedCommands();
    }
}
#endif

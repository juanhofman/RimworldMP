#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.UI;
using UnityEngine;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Non-DevMode entry: F10 toggles lobby; brand chip above main tabs; desync freeze overlay.
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
}
#endif

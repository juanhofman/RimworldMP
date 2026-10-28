#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.Lobby;
using UnityEngine;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Non-DevMode entry: F10 opens the lobby window; persistent corner button on entry + play UI roots.
/// Avoids fragile MainMenuDrawer / PauseMenu signature patches — re-check under RIMWORLD_DIR if desired.
/// </summary>
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
            Dialog_RimworldMPLobby.Open();
            Event.current.Use();
        }

        // Obvious always-on entry (main menu + pause/play) without DevMode archaeology.
        var btn = new Rect(12f, UI.screenHeight - 44f, 150f, 28f);
        if (Widgets.ButtonText(btn, "RimworldMP"))
            Dialog_RimworldMPLobby.Open();
    }
}
#endif

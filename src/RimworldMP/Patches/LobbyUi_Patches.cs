#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.UI;
using UnityEngine;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Non-DevMode entry: F10 toggles lobby; corner brand button; desync freeze overlay.
/// Avoids fragile MainMenuDrawer / PauseMenu signature patches — re-check under RIMWORLD_DIR.
/// </summary>
/// <remarks>
/// UNVERIFIED (RW 1.7): UIRoot.UIRootOnGUI + UnityEngine.Event / Widgets.ButtonText under Waylith stubs.
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

        // Desync veil first so it sits under the corner button / open windows as needed.
        DesyncFreezeOverlay.DrawIfNeeded();

        // Always-on brand entry (main menu + pause/play) without DevMode.
        var btn = new Rect(12f, UI.screenHeight - 44f, 150f, 28f);
        var prev = GUI.color;
        GUI.color = new Color(MpUiPalette.Ember.R, MpUiPalette.Ember.G, MpUiPalette.Ember.B, 1f);
        if (Widgets.ButtonText(btn, MpUiPalette.BrandName))
            LobbyUi.Open();
        GUI.color = prev;
    }
}
#endif

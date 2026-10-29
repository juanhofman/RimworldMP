#if !RIMWORLDMP_NO_GAME_REFS
using RimworldMP.Core.Session;
using UnityEngine;
using Verse;

namespace RimworldMP.UI;

/// <summary>
/// Full-screen readable desync freeze banner drawn from UIRoot OnGUI.
/// Intentionally calm — charcoal veil, ember headline, steel detail — not a DebugMenu scrap.
/// </summary>
public static class DesyncFreezeOverlay
{
    private static float _fadeIn;

    public static void DrawIfNeeded()
    {
        var session = SessionFacade.Current;
        if (session is null || !session.IsFrozen)
        {
            _fadeIn = 0f;
            return;
        }

        var model = DesyncOverlayModel.From(session.Desync.LastDesync)
                    ?? new DesyncOverlayModel
                    {
                        Tick = session.Tick,
                        LocalHashShort = "—",
                        RemoteHashShort = "—",
                        LogPath = "(no dump)",
                        Headline = "Desync — simulation frozen",
                        Body = "Peers diverged. Open the lobby (F10) to disconnect.",
                    };

        _fadeIn = Mathf.Min(1f, _fadeIn + Time.unscaledDeltaTime * 2.2f);
        var a = _fadeIn;

        var screen = new Rect(0f, 0f, UI.screenWidth, UI.screenHeight);
        Widgets.DrawBoxSolid(screen, new Color(MpUiPalette.Ink.R, MpUiPalette.Ink.G, MpUiPalette.Ink.B, 0.72f * a));

        var panelW = Mathf.Min(520f, UI.screenWidth - 40f);
        var panelH = 210f;
        var panel = new Rect(
            (UI.screenWidth - panelW) * 0.5f,
            (UI.screenHeight - panelH) * 0.38f,
            panelW,
            panelH);
        Widgets.DrawBoxSolid(panel, new Color(MpUiPalette.Panel.R, MpUiPalette.Panel.G, MpUiPalette.Panel.B, 0.98f * a));

        // Ember accent bar.
        Widgets.DrawBoxSolid(new Rect(panel.x, panel.y, 4f, panel.height),
            new Color(MpUiPalette.Ember.R, MpUiPalette.Ember.G, MpUiPalette.Ember.B, a));

        Text.Font = GameFont.Medium;
        GUI.color = new Color(MpUiPalette.Ember.R, MpUiPalette.Ember.G, MpUiPalette.Ember.B, a);
        Widgets.Label(new Rect(panel.x + 18f, panel.y + 16f, panel.width - 28f, 32f), MpUiPalette.BrandName);
        GUI.color = new Color(MpUiPalette.Danger.R, MpUiPalette.Danger.G, MpUiPalette.Danger.B, a);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(panel.x + 18f, panel.y + 48f, panel.width - 28f, 24f), model.Headline);

        GUI.color = new Color(MpUiPalette.Soft.R, MpUiPalette.Soft.G, MpUiPalette.Soft.B, a);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(panel.x + 18f, panel.y + 78f, panel.width - 28f, 90f), model.Body);

        GUI.color = new Color(MpUiPalette.Steel.R, MpUiPalette.Steel.G, MpUiPalette.Steel.B, a);
        Widgets.Label(new Rect(panel.x + 18f, panel.yMax - 36f, panel.width - 28f, 20f),
            $"Dump · {Truncate(model.LogPath, 64)}");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : "…" + s[^(max - 1)..];
}
#endif

#if !RIMWORLDMP_NO_GAME_REFS
using RimworldMP.Core.Session;
using RimworldMP.Lobby;
using UnityEngine;
using Verse;

namespace RimworldMP.UI;

/// <summary>
/// Full-screen readable desync freeze banner drawn from UIRoot OnGUI.
/// Intentionally calm — charcoal veil, ember headline, steel detail — not a DebugMenu scrap.
/// Continue triggers soft-recover (host clears freeze + resync peers; client requests join point).
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
                    ?? DesyncOverlayModel.Fallback(session.Tick);

        _fadeIn = Mathf.Min(1f, _fadeIn + Time.unscaledDeltaTime * 2.2f);
        var a = _fadeIn;

        // Namespace RimworldMP.UI shadows Verse.UI; Waylith dropped screenWidth/Height — use Screen.
        var sw = Screen.width;
        var sh = Screen.height;
        var screen = new Rect(0f, 0f, sw, sh);
        Widgets.DrawBoxSolid(screen, new Color(MpUiPalette.Ink.R, MpUiPalette.Ink.G, MpUiPalette.Ink.B, 0.72f * a));

        var panelW = Mathf.Min(560f, sw - 40f);
        var panelH = 292f;
        var panel = new Rect(
            (sw - panelW) * 0.5f,
            (sh - panelH) * 0.34f,
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
        Widgets.Label(new Rect(panel.x + 18f, panel.y + 78f, panel.width - 28f, 110f), model.Body);

        GUI.color = new Color(MpUiPalette.Steel.R, MpUiPalette.Steel.G, MpUiPalette.Steel.B, a);
        Widgets.Label(new Rect(panel.x + 18f, panel.yMax - 78f, panel.width - 28f, 20f),
            SessionMessages.Format("RMP.DesyncDumpLabel", Truncate(model.LogPath, 72)));

        var btnW = (panel.width - 48f) * 0.5f;
        var btnH = 32f;
        var btnY = panel.yMax - 48f;
        var continueLabel = session.IsHost
            ? SessionMessages.Get("RMP.BtnContinueHost")
            : SessionMessages.Get("RMP.BtnContinueClient");
        if (MpImmediateGui.OkButton(new Rect(panel.x + 18f, btnY, btnW, btnH), continueLabel))
            session.SoftRecoverFromDesync();

        if (MpImmediateGui.SteelButton(
                new Rect(panel.x + 30f + btnW, btnY, btnW, btnH),
                SessionMessages.Get("RMP.BtnDisconnect")))
        {
            _ = MpLobbyActions.StopAsync();
        }

        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : "…" + s[^(max - 1)..];
}
#endif

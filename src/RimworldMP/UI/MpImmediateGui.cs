#if !RIMWORLDMP_NO_GAME_REFS
using RimworldMP.Core.Session;
using UnityEngine;
using Verse;

namespace RimworldMP.UI;

/// <summary>
/// Immediate-GUI layout helpers for RimworldMP lobby / overlay surfaces.
/// Charcoal / steel / ember palette only — maps cleanly onto Verse <see cref="Widgets"/>.
/// Re-verify rect metrics under <c>RIMWORLD_DIR</c> (GameFont sizing differs by DPI/locale).
/// </summary>
public static class MpImmediateGui
{
    public static Color ToColor(MpUiPalette.Rgba c) => new(c.R, c.G, c.B, c.A);

    public static Color WithAlpha(MpUiPalette.Rgba c, float a) => new(c.R, c.G, c.B, a);

    public static void DrawInkBackdrop(Rect rect) =>
        Widgets.DrawBoxSolid(rect, ToColor(MpUiPalette.Ink));

    public static void DrawPanel(Rect rect) =>
        Widgets.DrawBoxSolid(rect, ToColor(MpUiPalette.Panel));

    public static void DrawEmberAccentBar(Rect panel) =>
        Widgets.DrawBoxSolid(
            new Rect(panel.x, panel.y, LobbyLayoutMetrics.AccentBarWidth, panel.height),
            ToColor(MpUiPalette.Ember));

    public static void DrawHairline(float x, float y, float w, float alpha = 0.35f) =>
        Widgets.DrawBoxSolid(new Rect(x, y, w, 1f), WithAlpha(MpUiPalette.Steel, alpha));

    public static float DrawBrandHero(float x, float y, float w, string brand, string subtitle)
    {
        Text.Font = GameFont.Medium;
        GUI.color = ToColor(MpUiPalette.Ember);
        Widgets.Label(new Rect(x, y, w, LobbyLayoutMetrics.BrandHeight), brand);
        GUI.color = Color.white;
        y += LobbyLayoutMetrics.BrandHeight - 2f;
        Text.Font = GameFont.Tiny;
        GUI.color = ToColor(MpUiPalette.Steel);
        Widgets.Label(new Rect(x, y, w, LobbyLayoutMetrics.SubtitleHeight), subtitle);
        GUI.color = Color.white;
        y += LobbyLayoutMetrics.SubtitleHeight + 4f;
        DrawHairline(x, y, w);
        return y + LobbyLayoutMetrics.HairlineGap;
    }

    public static float DrawSectionLabel(float x, float y, float w, string label, MpUiPalette.Rgba? tint = null)
    {
        Text.Font = GameFont.Tiny;
        GUI.color = ToColor(tint ?? MpUiPalette.Steel);
        Widgets.Label(new Rect(x, y, w, LobbyLayoutMetrics.FieldLabelHeight), label);
        GUI.color = Color.white;
        return y + LobbyLayoutMetrics.FieldLabelHeight;
    }

    public static float DrawMetaLine(float x, float y, float w, string text, MpUiPalette.Rgba? tint = null)
    {
        Text.Font = GameFont.Tiny;
        GUI.color = ToColor(tint ?? MpUiPalette.Steel);
        Widgets.Label(new Rect(x, y, w, LobbyLayoutMetrics.MetaLineHeight), text);
        GUI.color = Color.white;
        return y + LobbyLayoutMetrics.MetaLineHeight;
    }

    public static float DrawSoftLine(float x, float y, float w, string text, GameFont font = GameFont.Small, MpUiPalette.Rgba? tint = null)
    {
        Text.Font = font;
        GUI.color = ToColor(tint ?? MpUiPalette.Soft);
        var h = font == GameFont.Tiny ? LobbyLayoutMetrics.MetaLineHeight : 22f;
        Widgets.Label(new Rect(x, y, w, h), text);
        GUI.color = Color.white;
        return y + h;
    }

    public static bool EmberButton(Rect rect, string label) => TintedButton(rect, label, MpUiPalette.Ember);

    public static bool SteelButton(Rect rect, string label) => TintedButton(rect, label, MpUiPalette.Steel);

    public static bool DangerButton(Rect rect, string label) => TintedButton(rect, label, MpUiPalette.Danger);

    public static bool OkButton(Rect rect, string label) => TintedButton(rect, label, MpUiPalette.Ok);

    private static bool TintedButton(Rect rect, string label, MpUiPalette.Rgba tint)
    {
        var prev = GUI.color;
        GUI.color = ToColor(tint);
        var hit = Widgets.ButtonText(rect, label);
        GUI.color = prev;
        return hit;
    }

    public static float DrawLivePulse(float x, float y, float w, float pulseT)
    {
        var pulse = 0.55f + 0.45f * Mathf.Sin(pulseT * 2.4f);
        var live = ToColor(MpUiPalette.Ok);
        live.a = pulse;
        Widgets.DrawBoxSolid(new Rect(x, y, LobbyLayoutMetrics.LiveDotSize, LobbyLayoutMetrics.LiveDotSize), live);
        GUI.color = ToColor(MpUiPalette.Ok);
        Text.Font = GameFont.Tiny;
        Widgets.Label(
            new Rect(x + LobbyLayoutMetrics.LiveDotSize + 6f, y - 4f, w - LobbyLayoutMetrics.LiveDotSize - 6f, 18f),
            SessionMessages.Get("RMP.LiveBadge"));
        GUI.color = Color.white;
        return y + 20f;
    }

    public static string T(string key) => SessionMessages.Get(key);

    public static string Tf(string key, params object[] args) => SessionMessages.Format(key, args);
}
#endif

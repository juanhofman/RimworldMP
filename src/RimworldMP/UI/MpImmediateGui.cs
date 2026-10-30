#if !RIMWORLDMP_NO_GAME_REFS
using RimworldMP.Core.Session;
using UnityEngine;
using Verse;

namespace RimworldMP.UI;

/// <summary>
/// Immediate-GUI layout helpers for RimworldMP lobby / overlay / corner HUD.
/// Charcoal / steel / ember palette only — maps cleanly onto Verse <see cref="Widgets"/>.
/// Re-verify rect metrics under <c>RIMWORLD_DIR</c> (GameFont sizing differs by DPI/locale).
/// </summary>
public static class MpImmediateGui
{
    public static Color ToColor(MpUiPalette.Rgba c) => new(c.R, c.G, c.B, c.A);

    public static Color WithAlpha(MpUiPalette.Rgba c, float a) => new(c.R, c.G, c.B, a);

    public static void DrawInkBackdrop(Rect rect) =>
        Widgets.DrawBoxSolid(rect, ToColor(MpUiPalette.Ink));

    public static void DrawPanel(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, ToColor(MpUiPalette.Panel));
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), ToColor(MpUiPalette.Hairline));
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), WithAlpha(MpUiPalette.Ink, 0.55f));
    }

    public static void DrawElevated(Rect rect) =>
        Widgets.DrawBoxSolid(rect, ToColor(MpUiPalette.Elevated));

    public static void DrawEmberAccentBar(Rect panel) =>
        Widgets.DrawBoxSolid(
            new Rect(panel.x, panel.y, LobbyLayoutMetrics.AccentBarWidth, panel.height),
            ToColor(MpUiPalette.Ember));

    public static void DrawHairline(float x, float y, float w, float alpha = 0.28f) =>
        Widgets.DrawBoxSolid(new Rect(x, y, w, 1f), WithAlpha(MpUiPalette.Steel, alpha));

    public static float DrawBrandHero(float x, float y, float w, string brand, string subtitle)
    {
        Text.Font = GameFont.Medium;
        GUI.color = ToColor(MpUiPalette.Ember);
        Widgets.Label(new Rect(x, y, w, LobbyLayoutMetrics.BrandHeight), brand);
        GUI.color = Color.white;
        y += LobbyLayoutMetrics.BrandHeight - 4f;
        Text.Font = GameFont.Tiny;
        GUI.color = ToColor(MpUiPalette.Steel);
        Widgets.Label(new Rect(x, y, w, LobbyLayoutMetrics.SubtitleHeight), subtitle);
        GUI.color = Color.white;
        y += LobbyLayoutMetrics.SubtitleHeight + 2f;
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
        var h = font == GameFont.Tiny ? LobbyLayoutMetrics.MetaLineHeight : 20f;
        Widgets.Label(new Rect(x, y, w, h), text);
        GUI.color = Color.white;
        return y + h;
    }

    /// <summary>Compact session strip — fewer lines than the old debug-style meta dump.</summary>
    public static float DrawStatusStrip(float x, float y, float w, string primary, string secondary, MpUiPalette.Rgba? primaryTint = null)
    {
        var strip = new Rect(x, y, w, 40f);
        DrawElevated(strip);
        Widgets.DrawBoxSolid(
            new Rect(strip.x, strip.y, LobbyLayoutMetrics.AccentBarWidth, strip.height),
            ToColor(MpUiPalette.Ember));
        Text.Font = GameFont.Tiny;
        GUI.color = ToColor(primaryTint ?? MpUiPalette.Soft);
        Widgets.Label(new Rect(strip.x + 10f, strip.y + 4f, strip.width - 14f, 16f), primary);
        GUI.color = ToColor(MpUiPalette.Steel);
        Widgets.Label(new Rect(strip.x + 10f, strip.y + 20f, strip.width - 14f, 16f), secondary);
        GUI.color = Color.white;
        return y + 44f;
    }

    public static bool EmberButton(Rect rect, string label) => FilledButton(rect, label, MpUiPalette.Ember, softFill: 0.22f);

    public static bool SteelButton(Rect rect, string label) => FilledButton(rect, label, MpUiPalette.Steel, softFill: 0.16f);

    public static bool DangerButton(Rect rect, string label) => FilledButton(rect, label, MpUiPalette.Danger, softFill: 0.20f);

    public static bool OkButton(Rect rect, string label) => FilledButton(rect, label, MpUiPalette.Ok, softFill: 0.24f);

    private static bool FilledButton(Rect rect, string label, MpUiPalette.Rgba tint, float softFill)
    {
        Widgets.DrawBoxSolid(rect, WithAlpha(tint, softFill));
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), WithAlpha(tint, 0.55f));
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), WithAlpha(MpUiPalette.Ink, 0.45f));
        var prev = GUI.color;
        GUI.color = ToColor(tint);
        var hit = Widgets.ButtonText(rect, label, drawBackground: false, doMouseoverSound: true);
        GUI.color = prev;
        return hit;
    }

    public static float DrawLivePulse(float x, float y, float w, float pulseT)
    {
        var pulse = 0.55f + 0.45f * Mathf.Sin(pulseT * 2.4f);
        var live = ToColor(MpUiPalette.Ok);
        live.a = pulse;
        Widgets.DrawBoxSolid(new Rect(x, y + 3f, LobbyLayoutMetrics.LiveDotSize, LobbyLayoutMetrics.LiveDotSize), live);
        GUI.color = ToColor(MpUiPalette.Ok);
        Text.Font = GameFont.Tiny;
        Widgets.Label(
            new Rect(x + LobbyLayoutMetrics.LiveDotSize + 6f, y - 1f, w - LobbyLayoutMetrics.LiveDotSize - 6f, 18f),
            SessionMessages.Get("RMP.LiveBadge"));
        GUI.color = Color.white;
        return y + 18f;
    }

    /// <summary>
    /// Brand chip above the Architect/main-tab bar — solid panel + ember accent, not a raw tinted Widget.
    /// </summary>
    public static bool DrawHudChip(Rect rect, string label, bool active = false)
    {
        Widgets.DrawBoxSolid(rect, ToColor(active ? MpUiPalette.Elevated : MpUiPalette.Panel));
        Widgets.DrawBoxSolid(
            new Rect(rect.x, rect.y, LobbyLayoutMetrics.HudChipAccentWidth, rect.height),
            ToColor(MpUiPalette.Ember));
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), ToColor(MpUiPalette.Hairline));
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = ToColor(MpUiPalette.Soft);
        Widgets.Label(
            new Rect(
                rect.x + LobbyLayoutMetrics.HudChipAccentWidth,
                rect.y,
                rect.width - LobbyLayoutMetrics.HudChipAccentWidth,
                rect.height),
            label);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        return Widgets.ButtonInvisible(rect);
    }

    public static Rect HudChipRect()
    {
        var y = Screen.height
                - LobbyLayoutMetrics.MainButtonBarHeight
                - LobbyLayoutMetrics.HudChipGapAboveBar
                - LobbyLayoutMetrics.HudChipHeight;
        return new Rect(
            LobbyLayoutMetrics.HudChipMarginX,
            y,
            LobbyLayoutMetrics.HudChipWidth,
            LobbyLayoutMetrics.HudChipHeight);
    }

    public static string T(string key) => SessionMessages.Get(key);

    public static string Tf(string key, params object[] args) => SessionMessages.Format(key, args);
}
#endif

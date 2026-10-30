namespace RimworldMP.UI;

/// <summary>
/// Shared layout constants for the in-mod lobby + corner HUD chip. Harness-safe (no Verse).
/// Live Immediate GUI maps these to <c>Rect</c> / <c>Widgets</c> under <c>RIMWORLD_DIR</c>.
/// </summary>
public static class LobbyLayoutMetrics
{
    public const float WindowWidth = 620f;
    public const float WindowHeight = 640f;
    public const float WindowMargin = 6f;
    public const float ContentPadX = 16f;
    public const float ContentPadTop = 6f;
    public const float ContentPadBottom = 8f;
    public const float BrandHeight = 28f;
    public const float SubtitleHeight = 16f;
    public const float HairlineGap = 8f;
    public const float MetaLineHeight = 16f;
    public const float SectionGap = 8f;
    public const float FieldLabelHeight = 14f;
    public const float FieldHeight = 26f;
    public const float FieldGap = 30f;
    public const float PeerRowHeight = 26f;
    public const float PeerAreaMin = 48f;
    public const float PeerAreaMax = 160f;
    public const float ChatAreaMin = 72f;
    public const float ChatAreaHeight = 96f;
    public const float ChatLineHeight = 15f;
    public const float ButtonHeight = 30f;
    public const float ButtonGap = 6f;
    public const float AccentBarWidth = 3f;
    public const float LiveDotSize = 7f;
    public const float FooterHeight = 22f;
    public const float KickButtonWidth = 48f;
    public const int ChatHistoryLines = 12;

    /// <summary>Classic RimWorld main-tab strip height (Architect / Work / …).</summary>
    public const float MainButtonBarHeight = 35f;

    /// <summary>Floating brand chip above the main-tab bar (not overlapping Architect).</summary>
    public const float HudChipWidth = 128f;
    public const float HudChipHeight = 28f;
    public const float HudChipGapAboveBar = 10f;
    public const float HudChipMarginX = 12f;
    public const float HudChipAccentWidth = 3f;
}

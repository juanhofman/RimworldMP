namespace RimworldMP.UI;

/// <summary>
/// Release lobby palette — charcoal / steel / ember from the UI design brief.
/// Float channels are 0–1 for Unity <c>Color</c>; hex strings for docs and harness asserts.
/// </summary>
public static class MpUiPalette
{
    public const string BrandName = "RimworldMP";

    public const string InkHex = "#0E1114";
    public const string PanelHex = "#161B20";
    public const string ElevatedHex = "#1C232A";
    public const string SteelHex = "#7EB0C8";
    public const string EmberHex = "#E07A3D";
    public const string SoftHex = "#C9D2D8";
    public const string DangerHex = "#D95C5C";
    public const string OkHex = "#6FBF8A";
    public const string WarnHex = "#D4A24C";

    public static readonly Rgba Ink = new(0.055f, 0.067f, 0.078f, 1f);
    public static readonly Rgba Panel = new(0.086f, 0.106f, 0.125f, 0.97f);
    public static readonly Rgba Elevated = new(0.110f, 0.137f, 0.165f, 0.98f);
    public static readonly Rgba Steel = new(0.494f, 0.690f, 0.784f, 1f);
    public static readonly Rgba Ember = new(0.878f, 0.478f, 0.239f, 1f);
    public static readonly Rgba Soft = new(0.788f, 0.824f, 0.847f, 1f);
    public static readonly Rgba Danger = new(0.851f, 0.361f, 0.361f, 1f);
    public static readonly Rgba Ok = new(0.435f, 0.749f, 0.541f, 1f);
    public static readonly Rgba Warn = new(0.831f, 0.635f, 0.298f, 1f);
    public static readonly Rgba Dim = new(0.45f, 0.50f, 0.55f, 1f);
    public static readonly Rgba Hairline = new(1f, 1f, 1f, 0.08f);

    public readonly record struct Rgba(float R, float G, float B, float A);
}

using RimworldMP.Core.Session;
using RimworldMP.Core.Sync;

namespace RimworldMP.UI;

/// <summary>Readable copy for the full-screen desync freeze overlay (EN/NL via <see cref="SessionMessages"/>).</summary>
public sealed class DesyncOverlayModel
{
    public required long Tick { get; init; }
    public required string LocalHashShort { get; init; }
    public required string RemoteHashShort { get; init; }
    public required string LogPath { get; init; }
    public required string Headline { get; init; }
    public required string Body { get; init; }

    public static DesyncOverlayModel? From(DesyncInfo? info)
    {
        if (info is null)
            return null;

        var modShort = string.IsNullOrEmpty(info.ModlistHashShort) || info.ModlistHashShort == "—"
            ? null
            : info.ModlistHashShort;
        var modsLine = modShort is null
            ? ""
            : SessionMessages.Format("RMP.DesyncModsLine", modShort);
        return new DesyncOverlayModel
        {
            Tick = info.Tick,
            LocalHashShort = ShortHash(info.LocalHashHex),
            RemoteHashShort = ShortHash(info.RemoteHashHex),
            LogPath = info.LogPath,
            Headline = SessionMessages.Get("RMP.DesyncHeadline"),
            Body = SessionMessages.Format(
                "RMP.DesyncBodyDetail",
                info.Tick,
                ShortHash(info.LocalHashHex),
                ShortHash(info.RemoteHashHex),
                modsLine,
                info.LogPath),
        };
    }

    public static DesyncOverlayModel Fallback(long tick) =>
        new()
        {
            Tick = tick,
            LocalHashShort = "—",
            RemoteHashShort = "—",
            LogPath = SessionMessages.Get("RMP.DesyncNoDump"),
            Headline = SessionMessages.Get("RMP.DesyncHeadline"),
            Body = SessionMessages.Get("RMP.DesyncBodyShort"),
        };

    public static string ShortHash(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
            return "—";
        return hex.Length <= 12 ? hex : hex[..12] + "…";
    }
}

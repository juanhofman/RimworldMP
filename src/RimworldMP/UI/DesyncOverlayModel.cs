using RimworldMP.Core.Sync;

namespace RimworldMP.UI;

/// <summary>Readable copy for the full-screen desync freeze overlay.</summary>
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

        return new DesyncOverlayModel
        {
            Tick = info.Tick,
            LocalHashShort = ShortHash(info.LocalHashHex),
            RemoteHashShort = ShortHash(info.RemoteHashHex),
            LogPath = info.LogPath,
            Headline = "Desync — simulation frozen",
            Body =
                $"Peers diverged at tick {info.Tick}.\n" +
                $"Local  {ShortHash(info.LocalHashHex)}\n" +
                $"Remote {ShortHash(info.RemoteHashHex)}\n" +
                "Open the lobby (F10) to disconnect, or inspect the dump log.",
        };
    }

    public static string ShortHash(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
            return "—";
        return hex.Length <= 12 ? hex : hex[..12] + "…";
    }
}

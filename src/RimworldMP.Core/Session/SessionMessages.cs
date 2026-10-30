using System.Globalization;

namespace RimworldMP.Core.Session;

/// <summary>
/// Offline-friendly localized strings for protocol / modlist / DLC mismatch and session QoL notices.
/// Defaults to English; call <see cref="LoadOptional"/> to merge a sibling <c>*.strings</c> file (NL optional).
/// RimWorld keyed XML under <c>mod/Languages/</c> mirrors these keys for in-game Verse.Translate.
/// </summary>
public static class SessionMessages
{
    public const string CultureEn = "en";
    public const string CultureNl = "nl";

    private static readonly object Gate = new();
    private static string _culture = CultureEn;
    private static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        ["RMP.ProtocolMismatch"] = "Protocol mismatch: client={0} host={1} (min={2}).",
        ["RMP.ModlistMismatch"] = "Modlist hash mismatch: client='{0}' host='{1}'.",
        ["RMP.DlcMismatch"] = "DLC mismatch: client='{0}' host='{1}'.",
        ["RMP.LobbyFull"] = "Lobby full (max {0}).",
        ["RMP.JoinRejected"] = "Host rejected join: {0} — {1}",
        ["RMP.PeerJoined"] = "{0} joined the lobby.",
        ["RMP.PeerLeft"] = "{0} left the session.",
        ["RMP.PeerKicked"] = "{0} was kicked from the session.",
        ["RMP.YouWereKicked"] = "You were kicked from the session: {0}",
        ["RMP.KickDefaultReason"] = "Removed by host.",
        ["RMP.ReadyWaiting"] = "Waiting for all peers ready with matching modlist…",
        ["RMP.ReadyBlockedUnpause"] = "Cannot unpause until the ready-check passes.",
        ["RMP.ChatEmpty"] = "(empty)",
        ["RMP.RecoveryRequested"] = "Recovery rejoin requested — waiting for host join point…",
        ["RMP.RecoverySent"] = "Host sent recovery join point to peer {0}.",
        ["RMP.DisplayNameDefault"] = "Player {0}",
        ["RMP.DisplayNameHost"] = "Host",
        ["RMP.DisplayNameYou"] = "You",
        ["RMP.DisplayNameYouHost"] = "You (host)",
        // Desync freeze overlay (outside lobby Immediate GUI).
        ["RMP.DesyncHeadline"] = "Desync — simulation frozen",
        ["RMP.DesyncBodyShort"] = "Peers diverged. Open the lobby (F10) to disconnect.",
        ["RMP.DesyncBodyDetail"] = "Peers diverged at tick {0}.\nLocal  {1}\nRemote {2}\n{3}Dump (local only):\n{4}\nOpen the lobby (F10) to disconnect.",
        ["RMP.DesyncModsLine"] = "Mods   {0}\n",
        ["RMP.DesyncNoDump"] = "(no dump)",
        ["RMP.DesyncDumpLabel"] = "Local dump · {0}",
    };

    private static Dictionary<string, string> _overlay = new(StringComparer.Ordinal);

    public static string Culture
    {
        get { lock (Gate) return _culture; }
    }

    public static void ResetToEnglish()
    {
        lock (Gate)
        {
            _culture = CultureEn;
            _overlay = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Merge key=value lines from a strings file (e.g. <c>SessionMessages.nl.strings</c>).
    /// Lines starting with <c>#</c> or empty are ignored. Does not require RimWorld.
    /// </summary>
    public static int LoadOptional(string path, string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            return 0;

        var overlay = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key.Length == 0)
                continue;
            overlay[key] = Unescape(value);
        }

        lock (Gate)
        {
            _culture = string.IsNullOrWhiteSpace(culture) ? CultureEn : culture;
            _overlay = overlay;
        }

        return overlay.Count;
    }

    public static string Get(string key)
    {
        lock (Gate)
        {
            if (_overlay.TryGetValue(key, out var over) && !string.IsNullOrEmpty(over))
                return over;
            return En.TryGetValue(key, out var en) ? en : key;
        }
    }

    public static string Format(string key, params object[] args)
    {
        var template = Get(key);
        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public static string ProtocolMismatch(byte clientVersion, byte hostVersion, byte minSupported) =>
        Format("RMP.ProtocolMismatch", clientVersion, hostVersion, minSupported);

    public static string ModlistMismatch(string clientHash, string hostHash) =>
        Format("RMP.ModlistMismatch", Truncate(clientHash), Truncate(hostHash));

    public static string DlcMismatch(string clientFingerprint, string hostFingerprint) =>
        Format("RMP.DlcMismatch", Truncate(clientFingerprint), Truncate(hostFingerprint));

    public static string LobbyFull(int maxClients) =>
        Format("RMP.LobbyFull", maxClients);

    public static string JoinRejected(string reason, string message) =>
        Format("RMP.JoinRejected", reason, message);

    public static string DefaultDisplayName(int playerId, bool isHostSeat) =>
        isHostSeat ? Get("RMP.DisplayNameHost") : Format("RMP.DisplayNameDefault", playerId);

    private static string Truncate(string? value)
    {
        value ??= string.Empty;
        return value.Length <= 16 ? value : value[..16] + "…";
    }

    private static string Unescape(string value) =>
        value.Replace("\\n", "\n", StringComparison.Ordinal).Replace("\\=", "=", StringComparison.Ordinal);
}

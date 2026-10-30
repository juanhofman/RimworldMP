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
        ["RMP.RecoveryHostContinued"] = "Host continued after desync — recovery join points sent to clients.",
        ["RMP.DisplayNameDefault"] = "Player {0}",
        ["RMP.DisplayNameHost"] = "Host",
        ["RMP.DisplayNameYou"] = "You",
        ["RMP.DisplayNameYouHost"] = "You (host)",
        ["RMP.DesyncHeadline"] = "Desync — simulation frozen",
        ["RMP.DesyncBodyShort"] = "Peers diverged. Continue to soft-recover, or disconnect.",
        ["RMP.DesyncBodyDetail"] = "Peers diverged at tick {0}.\nLocal  {1}\nRemote {2}\n{3}Dump (local only):\n{4}\nContinue soft-recovers; Disconnect leaves the session.",
        ["RMP.DesyncModsLine"] = "Mods   {0}\n",
        ["RMP.DesyncNoDump"] = "(no dump)",
        ["RMP.DesyncDumpLabel"] = "Local dump · {0}",
        ["RMP.BtnContinueHost"] = "Continue (resync peers)",
        ["RMP.BtnContinueClient"] = "Continue (request resync)",
        ["RMP.LobbySubtitle"] = "Shared colony · LAN · in-game host",
        ["RMP.LobbySession"] = "Session · {0}",
        ["RMP.RoleIdle"] = "Idle",
        ["RMP.RoleHosting"] = "Hosting",
        ["RMP.RoleJoined"] = "Joined",
        ["RMP.RoleKicked"] = "Kicked",
        ["RMP.RoleFrozen"] = "Frozen (desync)",
        ["RMP.RoleJip"] = "Join-in-progress…",
        ["RMP.LanIdle"] = "LAN idle",
        ["RMP.LanHosting"] = "Hosting ({0}) · {1} peer(s) · max {2}",
        ["RMP.LanConnected"] = "Connected as P{0} ({1})",
        ["RMP.LanNonLan"] = "In-session (non-LAN transport)",
        ["RMP.LanNotConnected"] = "Not connected",
        ["RMP.EndpointHost"] = "0.0.0.0:{0}",
        ["RMP.EndpointDefault"] = "— · default port {0}",
        ["RMP.EndpointLoopHost"] = "loopback host · port {0}",
        ["RMP.EndpointLoopClient"] = "loopback client · port {0}",
        ["RMP.EndpointUnset"] = "—:{0}",
        ["RMP.ProtocolLabel"] = "Protocol v{0} · max {1}",
        ["RMP.PeerReady"] = "Ready",
        ["RMP.PeerNotReady"] = "Not ready",
        ["RMP.ModsOk"] = "mods OK",
        ["RMP.ModsMismatch"] = "mods MISMATCH",
        ["RMP.ModsUnknown"] = "hash ?",
        ["RMP.StatusLive"] = "Session live. Ready / chat / kick available in lobby.",
        ["RMP.StatusLiveReady"] = "Lobby ready — begin play or unpause when everyone is set.",
        ["RMP.StatusJip"] = "Receiving join-in-progress save…",
        ["RMP.StatusDesync"] = "DESYNC at tick {0} — sim frozen. Dump: {1}",
        ["RMP.StatusIdleHint"] = "Host or join a LAN lobby. F10 toggles this window.",
        ["RMP.StatusHandoffError"] = "Handoff error: {0}",
        ["RMP.ChatHeader"] = "CHAT",
        ["RMP.ChatSend"] = "Send",
        ["RMP.ChatSent"] = "Chat sent",
        ["RMP.SystemPrefix"] = "system",
        ["RMP.PeersHeader"] = "Peers ({0})",
        ["RMP.PeersEmptyHint"] = "Peers — join a session",
        ["RMP.PeersNone"] = "No peers yet.",
        ["RMP.ColName"] = "Name",
        ["RMP.ColReady"] = "Ready",
        ["RMP.ColMods"] = "Mods",
        ["RMP.DisplayNameLabel"] = "DISPLAY NAME",
        ["RMP.HostIpLabel"] = "HOST IP (join)",
        ["RMP.PortLabel"] = "PORT",
        ["RMP.ModlistHashLabel"] = "MODLIST HASH",
        ["RMP.PlayingAs"] = "Playing as {0}",
        ["RMP.LiveBadge"] = "LIVE",
        ["RMP.HandoffHeader"] = "LAUNCHER HANDOFF",
        ["RMP.BtnHost"] = "Host",
        ["RMP.BtnJoin"] = "Join",
        ["RMP.BtnReady"] = "Ready up",
        ["RMP.BtnUnready"] = "Unready",
        ["RMP.BtnDisconnect"] = "Disconnect",
        ["RMP.BtnContinue"] = "Continue",
        ["RMP.BtnAbort"] = "Abort session",
        ["RMP.BtnBeginPlay"] = "Begin play",
        ["RMP.BtnKick"] = "Kick",
        ["RMP.BtnHandoff"] = "Check launcher handoff",
        ["RMP.FlashHosting"] = "Hosting on {0}…",
        ["RMP.FlashJoining"] = "Joining {0}:{1}…",
        ["RMP.FlashAborted"] = "Session aborted",
        ["RMP.FlashReady"] = "Marked ready",
        ["RMP.FlashUnready"] = "Marked not ready",
        ["RMP.FlashKicked"] = "Kicked {0}",
        ["RMP.FlashHandoffChecked"] = "Checked launcher-handoff.json",
        ["RMP.FlashBeginPlay"] = "Lobby ready — close and play. Unpause when ready.",
        ["RMP.BeginPlayNotice"] = "Host began play.",
        ["RMP.HandoffMissing"] = "No launcher-handoff.json — open lobby or use the LAN launcher.",
        ["RMP.HandoffConfigOnly"] = "Handoff applied (config only; auto-start off).",
        ["RMP.HandoffAutoHost"] = "Handoff auto-hosted on port {0}.",
        ["RMP.HandoffAutoJoin"] = "Handoff auto-joined {0}:{1}.",
        ["RMP.HandoffUnknownRole"] = "Unknown handoff role '{0}'.",
        ["RMP.HandoffUnknown"] = "Launcher handoff not checked yet.",
        ["RMP.ReadySummary"] = "Ready {0}/{1} · mods {2}",
        ["RMP.ReadySummaryMatch"] = "match",
        ["RMP.ReadySummaryMismatch"] = "mismatch",
        ["RMP.MetaEndpoint"] = "Endpoint  {0}",
        ["RMP.MetaModlist"] = "Modlist   {0}",
        ["RMP.MsgHosting"] = "RimworldMP: hosting on port {0}",
        ["RMP.MsgJoining"] = "RimworldMP: joining {0}:{1}",
        ["RMP.MsgStopped"] = "RimworldMP: session stopped",
        ["RMP.HashUnset"] = "(unset)",
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

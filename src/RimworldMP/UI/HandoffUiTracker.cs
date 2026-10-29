using RimworldMP.Core.Lobby;

namespace RimworldMP.UI;

/// <summary>Last launcher-handoff consume outcome for lobby status copy.</summary>
public enum HandoffUiKind
{
    /// <summary>No consume attempted yet this process.</summary>
    Unknown = 0,
    /// <summary>No handoff file at the expected path.</summary>
    Missing = 1,
    /// <summary>File read/ack'd; config applied; auto-start skipped.</summary>
    AppliedConfigOnly = 2,
    /// <summary>Auto-started as host.</summary>
    AutoHosted = 3,
    /// <summary>Auto-started as client.</summary>
    AutoJoined = 4,
    /// <summary>Malformed / unsupported protocol / IO failure.</summary>
    Failed = 5,
}

/// <summary>Process-wide handoff UI status (local-only; not networked).</summary>
public static class HandoffUiTracker
{
    private static readonly object Gate = new();
    private static HandoffUiSnapshot _current = HandoffUiSnapshot.Unknown;

    public static HandoffUiSnapshot Current
    {
        get { lock (Gate) return _current; }
    }

    public static void Reset()
    {
        lock (Gate)
            _current = HandoffUiSnapshot.Unknown;
    }

    public static void RecordMissing(string path)
    {
        Set(new HandoffUiSnapshot(
            HandoffUiKind.Missing,
            path,
            Detail: "No launcher-handoff.json — open lobby or use the LAN launcher.",
            Role: null,
            Host: null,
            Port: null,
            SessionName: null,
            ModlistHash: null));
    }

    public static void RecordFailed(string path, string message)
    {
        Set(new HandoffUiSnapshot(
            HandoffUiKind.Failed,
            path,
            Detail: message,
            Role: null,
            Host: null,
            Port: null,
            SessionName: null,
            ModlistHash: null));
    }

    public static void RecordConsumed(string path, LauncherHandoffDocument doc, bool autoStarted)
    {
        ArgumentNullException.ThrowIfNull(doc);
        HandoffUiKind kind;
        string detail;
        if (!autoStarted || !doc.AutoStartLobby)
        {
            kind = HandoffUiKind.AppliedConfigOnly;
            detail = "Handoff applied (config only; auto-start off).";
        }
        else if (doc.IsHostRole)
        {
            kind = HandoffUiKind.AutoHosted;
            detail = $"Handoff auto-hosted on port {doc.Port}.";
        }
        else if (doc.IsClientRole)
        {
            kind = HandoffUiKind.AutoJoined;
            var host = string.IsNullOrWhiteSpace(doc.Host) ? "127.0.0.1" : doc.Host!;
            detail = $"Handoff auto-joined {host}:{doc.Port}.";
        }
        else
        {
            kind = HandoffUiKind.Failed;
            detail = $"Unknown handoff role '{doc.Role}'.";
        }

        Set(new HandoffUiSnapshot(
            kind,
            path,
            Detail: detail,
            Role: doc.Role,
            Host: doc.Host,
            Port: doc.Port,
            SessionName: doc.SessionName,
            ModlistHash: doc.ModlistHash));
    }

    private static void Set(HandoffUiSnapshot snap)
    {
        lock (Gate)
            _current = snap;
    }
}

public readonly record struct HandoffUiSnapshot(
    HandoffUiKind Kind,
    string Path,
    string Detail,
    string? Role,
    string? Host,
    int? Port,
    string? SessionName,
    string? ModlistHash)
{
    public static HandoffUiSnapshot Unknown { get; } = new(
        HandoffUiKind.Unknown,
        LauncherHandoff.DefaultPath,
        "Launcher handoff not checked yet.",
        null,
        null,
        null,
        null,
        null);

    public bool IsError => Kind is HandoffUiKind.Failed;
    public bool IsOk => Kind is HandoffUiKind.AppliedConfigOnly or HandoffUiKind.AutoHosted or HandoffUiKind.AutoJoined;
}

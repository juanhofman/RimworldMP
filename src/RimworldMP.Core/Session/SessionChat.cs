using System.Collections.Concurrent;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Session;

/// <summary>Lightweight ring of synced chat / system notices (not part of the sim command stream).</summary>
public sealed class SessionChat
{
    public const int DefaultCapacity = 64;

    private readonly ConcurrentQueue<SessionChatLine> _lines = new();
    private readonly object _gate = new();
    private int _capacity = DefaultCapacity;

    public int Capacity
    {
        get { lock (_gate) return _capacity; }
        set { lock (_gate) _capacity = Math.Clamp(value, 8, 256); }
    }

    public void Clear()
    {
        while (_lines.TryDequeue(out _))
        {
            // drain
        }
    }

    public void Append(SessionChatLine line)
    {
        _lines.Enqueue(line);
        Trim();
    }

    public void AppendChat(int playerId, string displayName, string text, long tick = -1) =>
        Append(new SessionChatLine(
            Kind: SessionChatKind.Chat,
            PlayerId: playerId,
            DisplayName: displayName ?? string.Empty,
            Text: TruncateText(text),
            Tick: tick,
            Utc: DateTimeOffset.UtcNow));

    public void AppendSystem(SystemNoticeKind kind, string text, int playerId = -1, string? displayName = null, long tick = -1) =>
        Append(new SessionChatLine(
            Kind: SessionChatKind.System,
            PlayerId: playerId,
            DisplayName: displayName ?? string.Empty,
            Text: TruncateText(text),
            Tick: tick,
            Utc: DateTimeOffset.UtcNow,
            NoticeKind: kind));

    public IReadOnlyList<SessionChatLine> Snapshot() => _lines.ToArray();

    private void Trim()
    {
        var cap = Capacity;
        while (_lines.Count > cap && _lines.TryDequeue(out _))
        {
            // drop oldest
        }
    }

    private static string TruncateText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return SessionMessages.Get("RMP.ChatEmpty");
        text = text.Trim();
        return text.Length <= 240 ? text : text[..240];
    }
}

public enum SessionChatKind : byte
{
    Chat = 0,
    System = 1,
}

public readonly record struct SessionChatLine(
    SessionChatKind Kind,
    int PlayerId,
    string DisplayName,
    string Text,
    long Tick,
    DateTimeOffset Utc,
    SystemNoticeKind NoticeKind = default);

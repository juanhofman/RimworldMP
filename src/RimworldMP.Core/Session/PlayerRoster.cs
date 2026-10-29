using System.Collections.Concurrent;

namespace RimworldMP.Core.Session;

/// <summary>Tracks player display names for lobby + handoff (local cache; synced via PlayerInfo).</summary>
public sealed class PlayerRoster
{
    private readonly ConcurrentDictionary<int, string> _names = new();

    public void Reset() => _names.Clear();

    public void SetName(int playerId, string? displayName)
    {
        var cleaned = Sanitize(displayName);
        if (cleaned.Length == 0)
            _names.TryRemove(playerId, out _);
        else
            _names[playerId] = cleaned;
    }

    public void Remove(int playerId) => _names.TryRemove(playerId, out _);

    public string GetName(int playerId, bool isHostSeat = false)
    {
        if (_names.TryGetValue(playerId, out var name) && name.Length > 0)
            return name;
        return SessionMessages.DefaultDisplayName(playerId, isHostSeat || playerId == 0);
    }

    public bool TryGetName(int playerId, out string name) => _names.TryGetValue(playerId, out name!);

    public IReadOnlyDictionary<int, string> Snapshot() => new Dictionary<int, string>(_names);

    public static string Sanitize(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return string.Empty;
        var trimmed = displayName.Trim();
        if (trimmed.Length > 32)
            trimmed = trimmed[..32];
        // Strip control chars
        Span<char> buf = stackalloc char[trimmed.Length];
        var n = 0;
        foreach (var c in trimmed)
        {
            if (char.IsControl(c))
                continue;
            buf[n++] = c;
        }

        return n == 0 ? string.Empty : new string(buf[..n]);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimworldMP.Core.Lobby;

/// <summary>
/// Schema for <c>launcher-handoff.json</c> written by the external LAN launcher and
/// consumed once by the in-mod lobby. Keep fields stable — see docs/launcher-handoff.md.
/// </summary>
public sealed class LauncherHandoffDocument
{
    public const int CurrentProtocol = 1;

    [JsonPropertyName("protocol")]
    public int Protocol { get; set; } = CurrentProtocol;

    /// <summary><c>host</c> or <c>client</c>.</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "host";

    /// <summary>Join target (client) or advertise hint (host). IPv4/IPv6/hostname.</summary>
    [JsonPropertyName("host")]
    public string? Host { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; } = 30555;

    [JsonPropertyName("modlistHash")]
    public string? ModlistHash { get; set; }

    [JsonPropertyName("sessionName")]
    public string? SessionName { get; set; }

    /// <summary>Optional player display name for lobby + chat (additive protocol 1 field).</summary>
    [JsonPropertyName("playerDisplayName")]
    public string? PlayerDisplayName { get; set; }

    [JsonPropertyName("autoStartLobby")]
    public bool AutoStartLobby { get; set; } = true;

    [JsonPropertyName("launcherPid")]
    public int? LauncherPid { get; set; }

    [JsonPropertyName("maxClients")]
    public int? MaxClients { get; set; }

    /// <summary>Best-effort RimWorld version hint from the launcher (optional).</summary>
    [JsonPropertyName("rwBuild")]
    public string? RwBuild { get; set; }

    /// <summary>ISO-8601 UTC timestamp from the launcher (optional).</summary>
    [JsonPropertyName("createdUtc")]
    public string? CreatedUtc { get; set; }

    public bool IsHostRole =>
        string.Equals(Role, "host", StringComparison.OrdinalIgnoreCase);

    public bool IsClientRole =>
        string.Equals(Role, "client", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Read / acknowledge launcher handoff files from the standard local path.</summary>
public static class LauncherHandoff
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Windows: <c>%LocalAppData%/RimworldMP/launcher-handoff.json</c>.
    /// Linux: <c>~/.local/share/RimworldMP/launcher-handoff.json</c> (via LocalApplicationData).
    /// </summary>
    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RimworldMP");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "launcher-handoff.json");

    public static LauncherHandoffDocument? TryRead(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<LauncherHandoffDocument>(json, JsonOptions);
            if (doc is null)
                return null;
            if (doc.Protocol != LauncherHandoffDocument.CurrentProtocol)
                throw new InvalidOperationException(
                    $"Unsupported launcher-handoff protocol {doc.Protocol}; expected {LauncherHandoffDocument.CurrentProtocol}.");
            return doc;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Malformed launcher-handoff.json at {path}: {ex.Message}", ex);
        }
    }

    /// <summary>Rename handoff to <c>.ack</c> so it is not consumed twice.</summary>
    public static void Acknowledge(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            return;
        var ack = path + ".ack";
        try
        {
            if (File.Exists(ack))
                File.Delete(ack);
            File.Move(path, ack);
        }
        catch
        {
            // Best-effort; leave file if rename fails so the operator can inspect it.
        }
    }

    public static void Write(LauncherHandoffDocument doc, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        File.WriteAllText(path, json);
    }
}

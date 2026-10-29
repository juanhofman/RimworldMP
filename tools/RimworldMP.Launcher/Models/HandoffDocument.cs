using System.Text.Json.Serialization;

namespace RimworldMP.Launcher.Models;

/// <summary>
/// Local handoff written by the launcher for the RimworldMP mod to consume once.
/// Schema: docs/launcher-handoff.md (protocol 1).
/// </summary>
public sealed class HandoffDocument
{
    [JsonPropertyName("protocol")]
    public int Protocol { get; init; } = 1;

    /// <summary>"host" or "client".</summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>Host bind/advertise IP for clients; host may use "0.0.0.0".</summary>
    [JsonPropertyName("host")]
    public required string Host { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; } = 30555;

    [JsonPropertyName("modlistHash")]
    public required string ModlistHash { get; init; }

    [JsonPropertyName("sessionName")]
    public string SessionName { get; init; } = "RimworldMP";

    [JsonPropertyName("autoStartLobby")]
    public bool AutoStartLobby { get; init; } = true;

    [JsonPropertyName("launcherPid")]
    public int LauncherPid { get; init; }

    /// <summary>Optional host cap (protocol 1 additive). In-mod default 4.</summary>
    [JsonPropertyName("maxClients")]
    public int? MaxClients { get; init; }

    [JsonPropertyName("rwBuild")]
    public string? RwBuild { get; init; }

    [JsonPropertyName("createdUtc")]
    public string CreatedUtc { get; init; } = DateTime.UtcNow.ToString("o");
}

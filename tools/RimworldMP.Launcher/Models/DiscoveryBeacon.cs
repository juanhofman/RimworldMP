using System.Text.Json.Serialization;

namespace RimworldMP.Launcher.Models;

/// <summary>UDP LAN discovery beacon payload (discovery port, not game port).</summary>
public sealed class DiscoveryBeacon
{
    public const string Magic = "RMPD";

    [JsonPropertyName("magic")]
    public string MagicToken { get; init; } = Magic;

    [JsonPropertyName("protocol")]
    public int Protocol { get; init; } = 1;

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; } = 30555;

    [JsonPropertyName("rwBuild")]
    public string RwBuild { get; init; } = "1.7";

    [JsonPropertyName("modHashShort")]
    public required string ModHashShort { get; init; }

    [JsonPropertyName("modlistHash")]
    public required string ModlistHash { get; init; }

    [JsonPropertyName("host")]
    public string? Host { get; init; }

    /// <summary>Optional LAN mod-sync TCP port (default 30557). Additive for protocol 1.</summary>
    [JsonPropertyName("modSyncPort")]
    public int? ModSyncPort { get; init; }
}

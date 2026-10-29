using System.Text.Json.Serialization;

namespace RimworldMP.ModSync;

/// <summary>One installed / shareable RimWorld mod package.</summary>
public sealed record ModManifestEntry
{
    [JsonPropertyName("packageId")]
    public required string PackageId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>Absolute path on the host (not sent to clients for path abuse; host-local only).</summary>
    [JsonIgnore]
    public string? LocalPath { get; init; }

    /// <summary>SHA256 hex of the ordered relative file tree (content identity).</summary>
    [JsonPropertyName("contentHash")]
    public required string ContentHash { get; init; }

    [JsonPropertyName("workshopId")]
    public string? WorkshopId { get; init; }

    [JsonPropertyName("byteSize")]
    public long ByteSize { get; init; }
}

/// <summary>Ordered active MP modlist + per-mod content fingerprints.</summary>
public sealed class SessionModManifest
{
    public const int SchemaVersion = 1;

    [JsonPropertyName("schema")]
    public int Schema { get; init; } = SchemaVersion;

    [JsonPropertyName("modlistHash")]
    public required string ModlistHash { get; init; }

    [JsonPropertyName("rwBuild")]
    public string RwBuild { get; init; } = "1.7";

    /// <summary>Active mods in load order (packageIds).</summary>
    [JsonPropertyName("orderedPackageIds")]
    public required IReadOnlyList<string> OrderedPackageIds { get; init; }

    [JsonPropertyName("mods")]
    public required IReadOnlyList<ModManifestEntry> Mods { get; init; }
}

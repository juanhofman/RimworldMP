namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// Local mod inventory for the Mods screen and LAN sync.
/// Implementation owned by the mod-sync agent — see docs/launcher-mod-manager-hooks.md.
/// </summary>
public interface IModCatalog
{
    /// <summary>Ordered active packageIds (load order), if ModsConfig is readable.</summary>
    IReadOnlyList<string> GetActivePackageIds();

    /// <summary>Installed mods known to the catalog (Workshop + local Mods folders).</summary>
    IReadOnlyList<ModCatalogEntry> ListInstalled();

    /// <summary>Resolve a packageId to an on-disk folder, or null if missing.</summary>
    ModCatalogEntry? FindByPackageId(string packageId);

    /// <summary>Same algorithm as Core <c>ModlistHash</c> / <see cref="ModlistHasher"/>.</summary>
    string ComputeActiveHash();
}

public sealed class ModCatalogEntry
{
    public required string PackageId { get; init; }
    public string? DisplayName { get; init; }
    public string? FolderPath { get; init; }
    public string? VersionHint { get; init; }
    public ModCatalogSource Source { get; init; }
}

public enum ModCatalogSource
{
    Unknown,
    Official,
    Workshop,
    Local,
}

using RimworldMP.ModSync;

namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// Real <see cref="IModCatalog"/>: ModsConfig active list + Mods/Workshop folder scan via ModSync library.
/// </summary>
public sealed class InstalledModCatalog : IModCatalog
{
    private readonly ModsConfigService _mods;
    private readonly Func<string?> _resolveInstallDir;

    public InstalledModCatalog(ModsConfigService? mods = null, Func<string?>? resolveInstallDir = null)
    {
        _mods = mods ?? new ModsConfigService();
        _resolveInstallDir = resolveInstallDir ?? (() => RimWorldLocator.TryLocate()?.InstallDir);
    }

    public IReadOnlyList<string> GetActivePackageIds()
    {
        var path = _mods.ResolveModsConfigPath();
        if (path is null || !File.Exists(path))
            return Array.Empty<string>();
        return _mods.Read(path).ActiveMods;
    }

    public IReadOnlyList<ModCatalogEntry> ListInstalled()
    {
        var installDir = _resolveInstallDir();
        if (string.IsNullOrWhiteSpace(installDir))
        {
            // Fallback: active list only (no disk paths)
            return GetActivePackageIds()
                .Select(id => new ModCatalogEntry
                {
                    PackageId = id,
                    DisplayName = id,
                    Source = GuessSource(id, workshopId: null, localPath: null),
                })
                .ToList();
        }

        var scanned = ModInventory.ScanInstalled(installDir);
        return scanned
            .Select(m => new ModCatalogEntry
            {
                PackageId = m.PackageId,
                DisplayName = string.IsNullOrWhiteSpace(m.Name) ? m.PackageId : m.Name,
                FolderPath = m.LocalPath,
                VersionHint = m.Version,
                Source = GuessSource(m.PackageId, m.WorkshopId, m.LocalPath),
            })
            .ToList();
    }

    public ModCatalogEntry? FindByPackageId(string packageId)
    {
        return ListInstalled()
            .FirstOrDefault(e => e.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase));
    }

    public string ComputeActiveHash() => ModlistHasher.Compute(GetActivePackageIds());

    private static ModCatalogSource GuessSource(string packageId, string? workshopId, string? localPath)
    {
        if (packageId.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase))
            return ModCatalogSource.Official;
        if (!string.IsNullOrWhiteSpace(workshopId))
            return ModCatalogSource.Workshop;
        if (!string.IsNullOrWhiteSpace(localPath) &&
            localPath.Contains($"{Path.DirectorySeparatorChar}workshop{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            return ModCatalogSource.Workshop;
        if (!string.IsNullOrWhiteSpace(localPath))
            return ModCatalogSource.Local;
        return ModCatalogSource.Unknown;
    }
}

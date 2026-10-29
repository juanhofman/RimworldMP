namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// Thin catalog backed by <see cref="ModsConfigService"/> active list only.
/// Replace/extend when the mod-sync agent adds Workshop/local folder scanning.
/// </summary>
public sealed class StubModCatalog : IModCatalog
{
    private readonly ModsConfigService _mods;

    public StubModCatalog(ModsConfigService? mods = null)
    {
        _mods = mods ?? new ModsConfigService();
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
        // TODO(mod-sync): scan Steam Workshop + Mods folders; map About.xml packageIds.
        return GetActivePackageIds()
            .Select(id => new ModCatalogEntry
            {
                PackageId = id,
                DisplayName = id,
                Source = GuessSource(id),
            })
            .ToList();
    }

    public ModCatalogEntry? FindByPackageId(string packageId)
    {
        return ListInstalled()
            .FirstOrDefault(e => e.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase));
    }

    public string ComputeActiveHash() => ModlistHasher.Compute(GetActivePackageIds());

    private static ModCatalogSource GuessSource(string packageId)
    {
        if (packageId.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase))
            return ModCatalogSource.Official;
        return ModCatalogSource.Unknown;
    }
}

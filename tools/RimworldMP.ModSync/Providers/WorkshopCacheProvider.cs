namespace RimworldMP.ModSync.Providers;

/// <summary>
/// Secondary source: copy from local Steam Workshop content folder when already cached
/// (<c>steamapps/workshop/content/294100/&lt;id&gt;</c>).
/// </summary>
public sealed class WorkshopCacheProvider : IModAcquisitionProvider
{
    private readonly IReadOnlyList<string> _workshopRoots;

    public WorkshopCacheProvider(IEnumerable<string> workshopRoots)
    {
        _workshopRoots = workshopRoots.ToList();
    }

    public WorkshopCacheProvider(string? rimWorldInstallDir)
        : this(ModInventory.CandidateWorkshopRoots(rimWorldInstallDir))
    {
    }

    public string Name => "steam-workshop-cache";

    public Task<string?> TryResolveLocalFolderAsync(
        ModManifestEntry needed,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(needed.WorkshopId))
        {
            foreach (var root in _workshopRoots)
            {
                var candidate = Path.Combine(root, needed.WorkshopId);
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "About", "About.xml")))
                {
                    return Task.FromResult<string?>(Path.GetFullPath(candidate));
                }
            }
        }

        // Fallback: scan workshop roots for matching packageId
        foreach (var root in _workshopRoots)
        {
            if (!Directory.Exists(root))
                continue;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var about = Path.Combine(dir, "About", "About.xml");
                var info = AboutXmlParser.TryParse(about);
                if (info is null)
                    continue;
                if (info.PackageId.Equals(needed.PackageId, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult<string?>(Path.GetFullPath(dir));
            }
        }

        return Task.FromResult<string?>(null);
    }
}

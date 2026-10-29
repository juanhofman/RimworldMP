namespace RimworldMP.ModSync;

/// <summary>Scan RimWorld Mods + Steam Workshop content folders into manifests.</summary>
public static class ModInventory
{
    public const int SteamAppId = 294100;

    public static IReadOnlyList<string> CandidateModsRoots(string? rimWorldInstallDir)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(rimWorldInstallDir))
        {
            list.Add(Path.Combine(rimWorldInstallDir, "Mods"));
        }

        return list;
    }

    public static IReadOnlyList<string> CandidateWorkshopRoots(string? rimWorldInstallDir)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(rimWorldInstallDir))
        {
            // .../steamapps/common/RimWorld → .../steamapps/workshop/content/294100
            var common = Path.GetDirectoryName(rimWorldInstallDir.TrimEnd(Path.DirectorySeparatorChar));
            var steamapps = common is null ? null : Path.GetDirectoryName(common);
            if (steamapps is not null)
            {
                list.Add(Path.Combine(steamapps, "workshop", "content", SteamAppId.ToString()));
            }
        }

        return list;
    }

    public static IReadOnlyList<ModManifestEntry> ScanInstalled(
        string? rimWorldInstallDir,
        IEnumerable<string>? extraModRoots = null)
    {
        var roots = new List<string>();
        roots.AddRange(CandidateModsRoots(rimWorldInstallDir));
        if (extraModRoots is not null)
            roots.AddRange(extraModRoots);

        var byPackage = new Dictionary<string, ModManifestEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var about = Path.Combine(dir, "About", "About.xml");
                var info = AboutXmlParser.TryParse(about);
                if (info is null)
                    continue;

                var workshopFromFolder = GuessWorkshopIdFromPath(dir);
                var workshop = workshopFromFolder
                    ?? AboutXmlParser.TryExtractWorkshopId(info.WorkshopUrl);

                var entry = new ModManifestEntry
                {
                    PackageId = info.PackageId,
                    Name = info.Name,
                    Version = info.Version,
                    LocalPath = Path.GetFullPath(dir),
                    ContentHash = ContentHasher.HashDirectory(dir),
                    WorkshopId = workshop,
                    ByteSize = ContentHasher.MeasureByteSize(dir),
                };
                byPackage[info.PackageId] = entry;
            }
        }

        // Workshop content folders are named by publishedfileid, not packageId.
        foreach (var workshopRoot in CandidateWorkshopRoots(rimWorldInstallDir))
        {
            if (!Directory.Exists(workshopRoot))
                continue;

            foreach (var dir in Directory.EnumerateDirectories(workshopRoot))
            {
                var about = Path.Combine(dir, "About", "About.xml");
                var info = AboutXmlParser.TryParse(about);
                if (info is null)
                    continue;

                var folderId = Path.GetFileName(dir);
                var workshopId = ulong.TryParse(folderId, out _) ? folderId : null;
                workshopId ??= AboutXmlParser.TryExtractWorkshopId(info.WorkshopUrl);

                if (byPackage.ContainsKey(info.PackageId))
                    continue; // local Mods wins

                byPackage[info.PackageId] = new ModManifestEntry
                {
                    PackageId = info.PackageId,
                    Name = info.Name,
                    Version = info.Version,
                    LocalPath = Path.GetFullPath(dir),
                    ContentHash = ContentHasher.HashDirectory(dir),
                    WorkshopId = workshopId,
                    ByteSize = ContentHasher.MeasureByteSize(dir),
                };
            }
        }

        return byPackage.Values
            .OrderBy(m => m.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static SessionModManifest BuildSessionManifest(
        IReadOnlyList<string> orderedPackageIds,
        IReadOnlyList<ModManifestEntry> installed,
        string? rwBuild = "1.7")
    {
        var map = installed.ToDictionary(m => m.PackageId, StringComparer.OrdinalIgnoreCase);
        var mods = new List<ModManifestEntry>();
        foreach (var id in orderedPackageIds)
        {
            if (!map.TryGetValue(id, out var entry))
            {
                // Official Ludeon content is not transferred via LAN mod sync.
                if (IsOfficialContent(id))
                    continue;

                throw new InvalidOperationException(
                    $"Active mod '{id}' is not installed locally (missing About.xml under Mods/Workshop).");
            }

            if (IsOfficialContent(id))
                continue;

            mods.Add(entry);
        }

        return new SessionModManifest
        {
            ModlistHash = ModlistHashMirror.Compute(orderedPackageIds),
            RwBuild = rwBuild ?? "1.7",
            OrderedPackageIds = orderedPackageIds.ToList(),
            Mods = mods,
        };
    }

    public static bool IsOfficialContent(string packageId) =>
        packageId.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase);

    private static string? GuessWorkshopIdFromPath(string dir)
    {
        var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return ulong.TryParse(name, out _) ? name : null;
    }
}

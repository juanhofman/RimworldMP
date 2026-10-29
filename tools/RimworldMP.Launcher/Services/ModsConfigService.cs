using System.Globalization;
using System.Xml.Linq;

namespace RimworldMP.Launcher.Services;

public sealed class ModsConfigSnapshot
{
    public required string Path { get; init; }
    public required string? Version { get; init; }
    public required IReadOnlyList<string> ActiveMods { get; init; }
    public required IReadOnlyList<string> KnownExpansions { get; init; }
}

/// <summary>
/// Read/write ModsConfig.xml with backup/restore profiles.
/// Enforces Harmony first + RimworldMP.Core enabled for MP sessions.
/// </summary>
public sealed class ModsConfigService
{
    private static readonly HashSet<string> OfficialPackageIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ludeon.rimworld",
        "ludeon.rimworld.royalty",
        "ludeon.rimworld.ideology",
        "ludeon.rimworld.biotech",
        "ludeon.rimworld.anomaly",
        "ludeon.rimworld.odyssey",
    };

    public string? ResolveModsConfigPath(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return Path.GetFullPath(explicitPath);

        foreach (var candidate in LauncherPaths.CandidateModsConfigPaths())
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        // Prefer the primary candidate even if missing (first launch / fresh install)
        return LauncherPaths.CandidateModsConfigPaths().FirstOrDefault();
    }

    public ModsConfigSnapshot Read(string path)
    {
        if (!File.Exists(path))
        {
            return new ModsConfigSnapshot
            {
                Path = path,
                Version = "1.7",
                ActiveMods = Array.Empty<string>(),
                KnownExpansions = Array.Empty<string>(),
            };
        }

        var doc = XDocument.Load(path);
        var root = doc.Root ?? throw new InvalidOperationException($"Invalid ModsConfig at {path}");
        var version = root.Element("version")?.Value?.Trim();
        var active = ReadLiList(root.Element("activeMods"));
        var expansions = ReadLiList(root.Element("knownExpansions"));
        return new ModsConfigSnapshot
        {
            Path = path,
            Version = version,
            ActiveMods = active,
            KnownExpansions = expansions,
        };
    }

    public IReadOnlyList<string> EnsureMpLoadOrder(IEnumerable<string> activeMods)
    {
        var input = activeMods
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        input.RemoveAll(p => p.Equals(LauncherPaths.HarmonyPackageId, StringComparison.OrdinalIgnoreCase));
        input.RemoveAll(p => p.Equals(LauncherPaths.RimworldMpPackageId, StringComparison.OrdinalIgnoreCase));

        var official = input.Where(p => OfficialPackageIds.Contains(p)).ToList();
        var rest = input.Where(p => !OfficialPackageIds.Contains(p)).ToList();

        // If Core is missing from active list, insert it so official block is non-empty when possible.
        if (!official.Any(p => p.Equals(LauncherPaths.LudeonCorePackageId, StringComparison.OrdinalIgnoreCase)))
        {
            // Keep user's choice if they truly have no Core (unusual); don't invent it.
        }

        var ordered = new List<string> { LauncherPaths.HarmonyPackageId };
        ordered.AddRange(official);
        ordered.Add(LauncherPaths.RimworldMpPackageId);
        ordered.AddRange(rest);
        return ordered;
    }

    public string ComputeHash(IEnumerable<string> orderedPackageIds) =>
        ModlistHasher.Compute(orderedPackageIds);

    public string Backup(string modsConfigPath, string? label = null)
    {
        LauncherPaths.EnsureDataDirs();
        if (!File.Exists(modsConfigPath))
            throw new FileNotFoundException("ModsConfig.xml not found; cannot backup.", modsConfigPath);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var name = string.IsNullOrWhiteSpace(label) ? $"ModsConfig-{stamp}.xml" : $"ModsConfig-{label}-{stamp}.xml";
        var dest = Path.Combine(LauncherPaths.BackupsDir, name);
        File.Copy(modsConfigPath, dest, overwrite: false);

        // Also keep a stable "pre-mp" pointer for easy restore
        var latest = Path.Combine(LauncherPaths.BackupsDir, "ModsConfig.pre-mp.xml");
        File.Copy(modsConfigPath, latest, overwrite: true);
        return dest;
    }

    public void RestoreLatestPreMp(string modsConfigPath)
    {
        var latest = Path.Combine(LauncherPaths.BackupsDir, "ModsConfig.pre-mp.xml");
        if (!File.Exists(latest))
            throw new FileNotFoundException("No pre-MP backup found.", latest);
        Directory.CreateDirectory(Path.GetDirectoryName(modsConfigPath)!);
        File.Copy(latest, modsConfigPath, overwrite: true);
    }

    public void WriteActiveMods(string modsConfigPath, IReadOnlyList<string> orderedPackageIds, string? version = null, IReadOnlyList<string>? knownExpansions = null)
    {
        XDocument doc;
        if (File.Exists(modsConfigPath))
            doc = XDocument.Load(modsConfigPath);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(modsConfigPath)!);
            doc = new XDocument(new XElement("ModsConfigData"));
        }

        var root = doc.Root ?? new XElement("ModsConfigData");
        if (doc.Root is null)
            doc.Add(root);

        var ver = root.Element("version");
        if (ver is null)
        {
            ver = new XElement("version", version ?? "1.7");
            root.AddFirst(ver);
        }
        else if (!string.IsNullOrWhiteSpace(version))
        {
            ver.Value = version;
        }

        WriteLiList(root, "activeMods", orderedPackageIds);

        if (knownExpansions is not null)
            WriteLiList(root, "knownExpansions", knownExpansions);

        // Atomic-ish replace via temp file
        var tmp = modsConfigPath + ".rimworldmp.tmp";
        doc.Save(tmp);
        File.Copy(tmp, modsConfigPath, overwrite: true);
        File.Delete(tmp);
    }

    /// <summary>
    /// Backup current config, write MP-ordered list, save a named profile copy.
    /// Returns (ordered mods, hash, backup path).
    /// </summary>
    public (IReadOnlyList<string> Ordered, string Hash, string BackupPath) PrepareForMp(
        string? modsConfigPath = null,
        string profileName = "default")
    {
        LauncherPaths.EnsureDataDirs();
        var path = ResolveModsConfigPath(modsConfigPath)
            ?? throw new InvalidOperationException("Could not resolve ModsConfig.xml path.");

        ModsConfigSnapshot snap;
        string backupPath;
        if (File.Exists(path))
        {
            backupPath = Backup(path, profileName);
            snap = Read(path);
        }
        else
        {
            backupPath = "(none — ModsConfig.xml did not exist yet)";
            snap = new ModsConfigSnapshot
            {
                Path = path,
                Version = "1.7",
                ActiveMods = new[] { LauncherPaths.LudeonCorePackageId },
                KnownExpansions = Array.Empty<string>(),
            };
        }

        var ordered = EnsureMpLoadOrder(snap.ActiveMods);
        WriteActiveMods(path, ordered, snap.Version, snap.KnownExpansions);

        var profilePath = Path.Combine(LauncherPaths.ProfilesDir, $"{Sanitize(profileName)}.xml");
        File.Copy(path, profilePath, overwrite: true);

        var hash = ComputeHash(ordered);
        return (ordered, hash, backupPath);
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "default" : name.Trim();
    }

    private static List<string> ReadLiList(XElement? parent)
    {
        if (parent is null)
            return new List<string>();
        return parent.Elements("li")
            .Select(e => (e.Value ?? string.Empty).Trim())
            .Where(v => v.Length > 0)
            .ToList();
    }

    private static void WriteLiList(XElement root, string elementName, IEnumerable<string> values)
    {
        var el = root.Element(elementName);
        if (el is null)
        {
            el = new XElement(elementName);
            root.Add(el);
        }
        else
        {
            el.RemoveAll();
        }

        foreach (var v in values)
            el.Add(new XElement("li", v));
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;

namespace RimworldMP.Launcher.Services;

public sealed class RimWorldInstall
{
    public required string InstallDir { get; init; }
    public required string ExecutablePath { get; init; }
    public string? VersionHint { get; init; }
    public string Source { get; init; } = "unknown";
}

public sealed class LauncherSettings
{
    public string? ManualRimWorldPath { get; set; }
}

/// <summary>Locate RimWorld 1.7 via Steam libraryfolders.vdf heuristics + remembered manual path.</summary>
public static class RimWorldLocator
{
    public const int SteamAppId = 294100;

    private static readonly JsonSerializerOptions SettingsJsonOptions = new() { WriteIndented = true };

    public static LauncherSettings LoadSettings()
    {
        LauncherPaths.EnsureDataDirs();
        if (!File.Exists(LauncherPaths.SettingsPath))
            return new LauncherSettings();
        try
        {
            var json = File.ReadAllText(LauncherPaths.SettingsPath);
            return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    public static void SaveSettings(LauncherSettings settings)
    {
        LauncherPaths.EnsureDataDirs();
        var json = JsonSerializer.Serialize(settings, SettingsJsonOptions);
        File.WriteAllText(LauncherPaths.SettingsPath, json);
    }

    public static RimWorldInstall? TryLocate(string? manualOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(manualOverride))
        {
            var fromManual = TryFromDirectory(manualOverride.Trim(), "manual");
            if (fromManual is not null)
            {
                var settings = LoadSettings();
                settings.ManualRimWorldPath = fromManual.InstallDir;
                SaveSettings(settings);
                return fromManual;
            }
        }

        var remembered = LoadSettings().ManualRimWorldPath;
        if (!string.IsNullOrWhiteSpace(remembered))
        {
            var fromRemembered = TryFromDirectory(remembered, "settings");
            if (fromRemembered is not null)
                return fromRemembered;
        }

        foreach (var libraryRoot in EnumerateSteamLibraryRoots())
        {
            var common = Path.Combine(libraryRoot, "steamapps", "common", "RimWorld");
            var hit = TryFromDirectory(common, "steam");
            if (hit is not null)
                return hit;

            // Some libraries store path already as steamapps root
            var alt = Path.Combine(libraryRoot, "common", "RimWorld");
            hit = TryFromDirectory(alt, "steam");
            if (hit is not null)
                return hit;
        }

        return null;
    }

    public static RimWorldInstall? TryFromDirectory(string dir, string source)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return null;

        var exe = FindExecutable(dir);
        if (exe is null)
            return null;

        return new RimWorldInstall
        {
            InstallDir = Path.GetFullPath(dir),
            ExecutablePath = exe,
            VersionHint = ProbeVersionHint(dir),
            Source = source,
        };
    }

    public static string? FindExecutable(string installDir)
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var name in new[] { "RimWorldWin64.exe", "RimWorld.exe" })
            {
                var p = Path.Combine(installDir, name);
                if (File.Exists(p))
                    return p;
            }

            var any = Directory.EnumerateFiles(installDir, "RimWorld*.exe", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return any;
        }

        foreach (var name in new[] { "RimWorldLinux", "RimWorld", "RimWorld.x86_64" })
        {
            var p = Path.Combine(installDir, name);
            if (File.Exists(p))
                return p;
        }

        // Unity player fallback
        var linuxAny = Directory.EnumerateFiles(installDir, "RimWorld*", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Where(f => (File.GetAttributes(f) & FileAttributes.Directory) == 0)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return linuxAny;
    }

    public static IEnumerable<string> EnumerateSteamLibraryRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vdf in CandidateLibraryFoldersVdf())
        {
            if (!File.Exists(vdf))
                continue;
            string text;
            try { text = File.ReadAllText(vdf); }
            catch { continue; }

            foreach (var path in ParseLibraryPaths(text))
            {
                if (seen.Add(path))
                    yield return path;
            }

            // Parent of steamapps that contains this vdf
            var steamapps = Path.GetDirectoryName(vdf);
            if (steamapps is not null)
            {
                var root = Path.GetDirectoryName(steamapps);
                if (root is not null && seen.Add(root))
                    yield return root;
            }
        }
    }

    private static IEnumerable<string> CandidateLibraryFoldersVdf()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            yield return Path.Combine(pf, "Steam", "steamapps", "libraryfolders.vdf");
            var pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            yield return Path.Combine(pf64, "Steam", "steamapps", "libraryfolders.vdf");
            yield break;
        }

        yield return Path.Combine(home, ".local", "share", "Steam", "steamapps", "libraryfolders.vdf");
        yield return Path.Combine(home, ".steam", "steam", "steamapps", "libraryfolders.vdf");
        yield return Path.Combine(home, ".steam", "root", "steamapps", "libraryfolders.vdf");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam",
            "steamapps", "libraryfolders.vdf");
    }

    private static IEnumerable<string> ParseLibraryPaths(string vdf)
    {
        // Match "path"\t\t"/some/path" or "path" "/some/path"
        var matches = Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            var raw = m.Groups[1].Value.Replace("\\\\", "\\");
            if (!string.IsNullOrWhiteSpace(raw))
                yield return raw;
        }
    }

    private static string? ProbeVersionHint(string installDir)
    {
        // Best-effort: Version.txt / About sometimes present; else "1.7" assumed for MVP.
        foreach (var name in new[] { "Version.txt", "version.txt" })
        {
            var p = Path.Combine(installDir, name);
            if (File.Exists(p))
            {
                try
                {
                    var line = File.ReadLines(p).FirstOrDefault()?.Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                        return line;
                }
                catch { /* ignore */ }
            }
        }

        return "1.7";
    }
}

namespace RimworldMP.Launcher.Services;

/// <summary>Local-only paths for handoff, profiles, and ModsConfig backups. No network uploads.</summary>
public static class LauncherPaths
{
    public const string AppFolderName = "RimworldMP";
    public const string HandoffFileName = "launcher-handoff.json";
    public const string HandoffAckSuffix = ".ack";
    public const int DefaultGamePort = 30555;
    public const int DefaultDiscoveryPort = 30556;

    public const string HarmonyPackageId = "brrainz.harmony";
    public const string RimworldMpPackageId = "RimworldMP.Core";
    public const string LudeonCorePackageId = "ludeon.rimworld";

    public static string DataRoot
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, AppFolderName);
            }

            // Linux / macOS: XDG data home
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
                return Path.Combine(xdg, AppFolderName);

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".local", "share", AppFolderName);
        }
    }

    public static string HandoffPath => Path.Combine(DataRoot, HandoffFileName);

    public static string ProfilesDir => Path.Combine(DataRoot, "profiles");

    public static string BackupsDir => Path.Combine(DataRoot, "backups");

    public static string SettingsPath => Path.Combine(DataRoot, "launcher-settings.json");

    public static void EnsureDataDirs()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(BackupsDir);
    }

    /// <summary>Known Ludeon ModsConfig.xml locations (probe in order).</summary>
    public static IEnumerable<string> CandidateModsConfigPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(userProfile, "AppData", "LocalLow", "Ludeon Studios",
                "RimWorld by Ludeon Studios", "Config", "ModsConfig.xml");
            yield break;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Classic Unity config path (Linux)
        yield return Path.Combine(home, ".config", "unity3d", "Ludeon Studios",
            "RimWorld by Ludeon Studios", "Config", "ModsConfig.xml");
        // Some Waylith / packaging layouts
        yield return Path.Combine(home, ".config", "Ludeon Studios",
            "RimWorld by Ludeon Studios", "Config", "ModsConfig.xml");
        // Flatpak Steam / Unity
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "config", "unity3d",
            "Ludeon Studios", "RimWorld by Ludeon Studios", "Config", "ModsConfig.xml");

        if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Application Support", "Ludeon Studios",
                "RimWorld by Ludeon Studios", "Config", "ModsConfig.xml");
        }
    }
}

using System.Diagnostics;

namespace RimworldMP.Launcher.Services;

public sealed class LaunchResult
{
    public required int ProcessId { get; init; }
    public required string ExecutablePath { get; init; }
    public required string WorkingDirectory { get; init; }
    public string Method { get; init; } = "direct";
}

/// <summary>Start RimWorld from the install directory (Windows / Linux).</summary>
public static class ProcessLauncher
{
    public const int SteamAppId = RimWorldLocator.SteamAppId;

    public static LaunchResult Launch(RimWorldInstall install, IEnumerable<string>? extraArgs = null)
    {
        if (!File.Exists(install.ExecutablePath))
            throw new FileNotFoundException("RimWorld executable not found.", install.ExecutablePath);

        // RimWorld 1.7 Waylith ships RimWorld.exe. Steam's Play button still looks for
        // RimWorldWin64.exe on some clients — never use steam://rungameid here or Steam fails
        // with "Uitvoerbaar spelbestand ontbreekt". Launch the real binary directly.
        EnsureLegacySteamExeShim(install.InstallDir);

        var args = extraArgs?.ToArray() ?? Array.Empty<string>();
        var psi = new ProcessStartInfo
        {
            FileName = install.ExecutablePath,
            WorkingDirectory = install.InstallDir,
            UseShellExecute = OperatingSystem.IsWindows(),
        };

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start RimWorld process.");

        return new LaunchResult
        {
            ProcessId = proc.Id,
            ExecutablePath = install.ExecutablePath,
            WorkingDirectory = install.InstallDir,
            Method = "direct",
        };
    }

    /// <summary>
    /// Best-effort: if install has RimWorld.exe but not RimWorldWin64.exe, copy/link a shim so
    /// Steam's legacy launch config also works. Failures are ignored (no admin rights, etc.).
    /// </summary>
    public static void EnsureLegacySteamExeShim(string installDir)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(installDir))
            return;

        var modern = Path.Combine(installDir, "RimWorld.exe");
        var legacy = Path.Combine(installDir, "RimWorldWin64.exe");
        if (!File.Exists(modern) || File.Exists(legacy))
            return;

        try
        {
            File.Copy(modern, legacy, overwrite: false);
        }
        catch
        {
            // Program Files often needs elevation — launcher still starts RimWorld.exe directly.
        }
    }
}

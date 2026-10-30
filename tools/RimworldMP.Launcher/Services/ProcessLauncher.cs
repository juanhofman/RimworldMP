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

        var args = extraArgs?.ToArray() ?? Array.Empty<string>();

        // Prefer Steam protocol on Windows so DRM/overlay/workdir match a normal play session.
        // Extra args are not supported via steam:// — fall back to direct exe.
        if (OperatingSystem.IsWindows() && args.Length == 0 && TryLaunchViaSteam(out var steamPid))
        {
            return new LaunchResult
            {
                ProcessId = steamPid,
                ExecutablePath = install.ExecutablePath,
                WorkingDirectory = install.InstallDir,
                Method = "steam",
            };
        }

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

    private static bool TryLaunchViaSteam(out int processId)
    {
        processId = 0;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = $"steam://rungameid/{SteamAppId}",
                UseShellExecute = true,
            };
            var proc = Process.Start(psi);
            if (proc is null)
                return false;
            processId = proc.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

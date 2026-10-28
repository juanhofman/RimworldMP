using System.Diagnostics;

namespace RimworldMP.Launcher.Services;

public sealed class LaunchResult
{
    public required int ProcessId { get; init; }
    public required string ExecutablePath { get; init; }
    public required string WorkingDirectory { get; init; }
}

/// <summary>Start RimWorld from the install directory (Windows / Linux).</summary>
public static class ProcessLauncher
{
    public static LaunchResult Launch(RimWorldInstall install, IEnumerable<string>? extraArgs = null)
    {
        if (!File.Exists(install.ExecutablePath))
            throw new FileNotFoundException("RimWorld executable not found.", install.ExecutablePath);

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
        };
    }
}

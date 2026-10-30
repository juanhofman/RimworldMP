using Avalonia;
using RimworldMP.Launcher.Cli;
using RimworldMP.Launcher.Services;
using RimworldMP.Launcher.Services.ModSync;

namespace RimworldMP.Launcher;

internal static class Program
{
    private static readonly string[] CliCommands =
    {
        "help", "-h", "--help", "locate", "mods", "hash", "discover",
        "host", "join", "handoff-path", "self-test", "--cli",
    };

    [STAThread]
    public static int Main(string[] args)
    {
        ModlistHasher.AssertGoldenMatchesCoreExpectation();
        // Real LAN catalog + sync backends for Avalonia Mods screen (see docs/launcher-mod-manager-hooks.md).
        ModSyncServices.UseLanBackends();

        // Design-review capture: --capture-ui [outdir] (needs a display / Xvfb).
        if (TryGetCaptureOutDir(args, out var captureDir))
            return Gui.UiCapture.Run(captureDir);

        if (ShouldUseCli(args, out var cliArgs))
            return CliApp.RunAsync(cliArgs).GetAwaiter().GetResult();

        // MainWindow is created in Gui.App.OnFrameworkInitializationCompleted.
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static bool TryGetCaptureOutDir(string[] args, out string outDir)
    {
        outDir = "";
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--capture-ui", StringComparison.OrdinalIgnoreCase))
                continue;
            outDir = i + 1 < args.Length && !args[i + 1].StartsWith('-')
                ? args[i + 1]
                : Path.Combine(Environment.CurrentDirectory, "capture-ui");
            return true;
        }

        return false;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Gui.App>()
            .UsePlatformDetect()
            .With(new Avalonia.X11PlatformOptions
            {
                // Avoid DBus IME on headless / minimal Linux desktops (capture + cloud VMs).
                EnableIme = false,
                UseDBusMenu = false,
                UseDBusFilePicker = false,
            })
            .LogToTrace();

    private static bool ShouldUseCli(string[] args, out string[] cliArgs)
    {
        cliArgs = args;
        if (args.Any(a => a.Equals("--cli", StringComparison.OrdinalIgnoreCase)))
        {
            cliArgs = args.Where(a => !a.Equals("--cli", StringComparison.OrdinalIgnoreCase)).ToArray();
            return true;
        }

        if (args.Length > 0 && CliCommands.Contains(args[0], StringComparer.OrdinalIgnoreCase))
            return true;

        if (args.Any(a => a.Equals("--ui", StringComparison.OrdinalIgnoreCase)))
            return false;

        // Headless / no display → CLI help rather than crashing Avalonia
        if (OperatingSystem.IsLinux() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            Console.Error.WriteLine("No display detected — falling back to CLI. Pass --cli explicitly, or set DISPLAY.");
            cliArgs = args.Length == 0 ? new[] { "help" } : args;
            return true;
        }

        return false;
    }
}

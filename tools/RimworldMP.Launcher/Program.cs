using Avalonia;
using RimworldMP.Launcher.Cli;
using RimworldMP.Launcher.Services;

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

        if (ShouldUseCli(args, out var cliArgs))
            return CliApp.RunAsync(cliArgs).GetAwaiter().GetResult();

        // MainWindow is created in Gui.App.OnFrameworkInitializationCompleted.
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Gui.App>()
            .UsePlatformDetect()
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

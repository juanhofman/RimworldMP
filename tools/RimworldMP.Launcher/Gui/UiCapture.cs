using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RimworldMP.Launcher.Gui.Views;
using RimworldMP.Launcher.Models;
using RimworldMP.Launcher.Services;
using RimworldMP.Launcher.ViewModels;

namespace RimworldMP.Launcher.Gui;

/// <summary>
/// One-shot UI page capture for design review (Home / Host / Join / Mods / Launch).
/// Invoked via <c>--capture-ui &lt;outdir&gt;</c>; not part of the normal product CLI.
/// </summary>
internal static class UiCapture
{
    private static readonly (LauncherNavPage Page, string FileName)[] Pages =
    {
        (LauncherNavPage.Home, "home.png"),
        (LauncherNavPage.Host, "host.png"),
        (LauncherNavPage.Join, "join.png"),
        (LauncherNavPage.Mods, "mods.png"),
        (LauncherNavPage.Launch, "launch.png"),
    };

    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        App.SuppressDefaultMainWindow = true;
        var exit = 1;
        var builder = Program.BuildAvaloniaApp();
        builder.AfterSetup(_ =>
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await CaptureAllAsync(outDir).ConfigureAwait(true);
                    exit = 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"capture-ui failed: {ex}");
                    exit = 2;
                }
                finally
                {
                    if (Application.Current?.ApplicationLifetime is IControlledApplicationLifetime life)
                        life.Shutdown(exit);
                    else
                        Environment.Exit(exit);
                }
            });
        });

        builder.StartWithClassicDesktopLifetime(Array.Empty<string>());
        return exit;
    }

    private static async Task CaptureAllAsync(string outDir)
    {
        var window = App.BuildMainWindow();
        window.Width = 1080;
        window.Height = 680;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = new PixelPoint(40, 40);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = window;

        if (window.DataContext is not MainViewModel vm)
            throw new InvalidOperationException("MainWindow DataContext is not MainViewModel.");

        SeedDemoData(vm);

        window.Show();
        await WaitForRenderAsync().ConfigureAwait(true);
        await Task.Delay(350).ConfigureAwait(true);

        foreach (var (page, fileName) in Pages)
        {
            // Drive navigation the same way the sidebar buttons do.
            switch (page)
            {
                case LauncherNavPage.Home:
                    vm.GoHomeCommand.Execute(null);
                    break;
                case LauncherNavPage.Host:
                    vm.GoHostCommand.Execute(null);
                    break;
                case LauncherNavPage.Join:
                    vm.GoJoinCommand.Execute(null);
                    break;
                case LauncherNavPage.Mods:
                    // Avoid GoMods → RefreshModsSnapshot wiping capture seed when no ModsConfig exists.
                    vm.CurrentPage = LauncherNavPage.Mods;
                    EnsureDemoMods(vm);
                    break;
                case LauncherNavPage.Launch:
                    vm.GoLaunchCommand.Execute(null);
                    break;
            }

            if (vm.CurrentPage != page)
                throw new InvalidOperationException($"Nav failed: expected {page}, got {vm.CurrentPage}.");

            // Belt-and-suspenders: force page roots visible so RenderTargetBitmap cannot
            // capture a stale Home frame if binding/layout lags one tick.
            window.ForcePageForCapture(page);

            await WaitForRenderAsync().ConfigureAwait(true);
            await Task.Delay(450).ConfigureAwait(true);

            var path = Path.Combine(outDir, fileName);
            await SaveWindowPngAsync(window, path).ConfigureAwait(true);
            Console.WriteLine($"wrote {path} page={page} isHome={vm.IsHome} isHost={vm.IsHost} isJoin={vm.IsJoin} isMods={vm.IsMods} isLaunch={vm.IsLaunch}");
        }

        window.Close();
    }

    private static void SeedDemoData(MainViewModel vm)
    {
        if (vm.DiscoveredHosts.Count == 0)
        {
            vm.DiscoveredHosts.Add(new DiscoveryBeacon
            {
                Name = "Colony Night",
                Host = "192.168.1.20",
                Port = 30555,
                RwBuild = "1.7",
                ModlistHash = "f2046ab21e526aae4469bdf1418310b553a2c34b9b9b617cc148a00d76e963f6",
                ModHashShort = "f2046ab2",
            });
        }

        vm.LaunchLog = "Ready to play.";
        vm.InstallSummary = "RimWorld not found — set the install folder below.";
        EnsureDemoMods(vm);
    }

    private static void EnsureDemoMods(MainViewModel vm)
    {
        if (vm.ModRows.Count > 0 && vm.FilteredModRows.Count > 0)
            return;

        var demo = new[]
        {
            LauncherPaths.HarmonyPackageId,
            "Ludeon.RimWorld",
            LauncherPaths.RimworldMpPackageId,
            "dubwise.dubsbadhygiene",
            "vanillaexpanded.vee",
        };
        var rows = new List<ModListRow>();
        for (var i = 0; i < demo.Length; i++)
        {
            var id = demo[i];
            rows.Add(new ModListRow
            {
                Index = i + 1,
                PackageId = id,
                IsHarmony = id.Equals(LauncherPaths.HarmonyPackageId, StringComparison.OrdinalIgnoreCase),
                IsRimworldMp = id.Equals(LauncherPaths.RimworldMpPackageId, StringComparison.OrdinalIgnoreCase),
            });
        }

        vm.SeedModRowsForCapture(rows);
    }

    private static async Task WaitForRenderAsync()
    {
        // Flush layout + render passes so IsVisible page swaps are in the bitmap.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
    }

    private static async Task SaveWindowPngAsync(Window window, string path)
    {
        window.Width = 1080;
        window.Height = 680;
        window.InvalidateVisual();
        window.UpdateLayout();
        await WaitForRenderAsync().ConfigureAwait(true);

        var size = window.Bounds.Size;
        if (size.Width < 8 || size.Height < 8)
            size = new Size(1080, 680);

        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(size.Width)),
            Math.Max(1, (int)Math.Ceiling(size.Height)));

        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(path);
    }
}

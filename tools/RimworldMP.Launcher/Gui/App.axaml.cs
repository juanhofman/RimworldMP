using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RimworldMP.Launcher.Gui.Views;

namespace RimworldMP.Launcher.Gui;

public partial class App : Application
{
    /// <summary>When true, skip default MainWindow — <see cref="UiCapture"/> owns the window.</summary>
    internal static bool SuppressDefaultMainWindow { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (!SuppressDefaultMainWindow
            && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = BuildMainWindow();
        base.OnFrameworkInitializationCompleted();
    }

    public static MainWindow BuildMainWindow() => new();
}

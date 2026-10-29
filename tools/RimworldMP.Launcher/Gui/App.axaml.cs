using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RimworldMP.Launcher.Gui.Views;

namespace RimworldMP.Launcher.Gui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = BuildMainWindow();
        base.OnFrameworkInitializationCompleted();
    }

    public static MainWindow BuildMainWindow() => new();
}

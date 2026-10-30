using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using RimworldMP.Launcher.Models;
using RimworldMP.Launcher.ViewModels;

namespace RimworldMP.Launcher.Gui.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        _vm = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) =>
        {
            if (_vm is not null)
                _vm.PropertyChanged -= OnViewModelPropertyChanged;
        };
    }

    private void OnBeaconPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: DiscoveryBeacon beacon })
            return;
        if (DataContext is MainViewModel vm)
            vm.SelectBeaconCommand.Execute(beacon);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.CurrentPage))
            return;

        // Intentional motion #1: panel fade on nav.
        PageHostBorder.Opacity = 0.35;
        Dispatcher.UIThread.Post(() => PageHostBorder.Opacity = 1, DispatcherPriority.Background);
    }

    /// <summary>Used by <c>--capture-ui</c> so each PNG is forced to the intended page.</summary>
    internal void ForcePageForCapture(LauncherNavPage page)
    {
        PageHome.IsVisible = page == LauncherNavPage.Home;
        PageHost.IsVisible = page == LauncherNavPage.Host;
        PageJoin.IsVisible = page == LauncherNavPage.Join;
        PageMods.IsVisible = page == LauncherNavPage.Mods;
        PageLaunch.IsVisible = page == LauncherNavPage.Launch;
        PageHostBorder.Opacity = 1;
        UpdateLayout();
    }
}

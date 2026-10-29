using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RimworldMP.Launcher.Models;
using RimworldMP.Launcher.ViewModels;

namespace RimworldMP.Launcher.Gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void OnBeaconPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: DiscoveryBeacon beacon })
            return;
        if (DataContext is MainViewModel vm)
            vm.SelectBeaconCommand.Execute(beacon);
    }
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.Data;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class ConnectView : Page
{
    private ConnectViewModel _vm = null!;
    private System.Windows.Media.Animation.Storyboard? _laserStoryboard;
    private System.Windows.Media.Animation.Storyboard? _spinStoryboard;

    public ConnectView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetRequiredService<ConnectViewModel>();
        DataContext = _vm;

        _laserStoryboard = Resources.Contains("LaserFlowAnimation")
            ? (System.Windows.Media.Animation.Storyboard)Resources["LaserFlowAnimation"]
            : null;

        _spinStoryboard = Resources.Contains("SpinAnimation")
            ? (System.Windows.Media.Animation.Storyboard)Resources["SpinAnimation"]
            : null;

        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectViewModel.CurrentFlowStep) ||
                args.PropertyName == nameof(ConnectViewModel.IsConnected) ||
                args.PropertyName == nameof(ConnectViewModel.IsRoutesApplied) ||
                args.PropertyName == nameof(ConnectViewModel.IsGlobalOptimizationActive))
            {
                Dispatcher.Invoke(() =>
                {
                    if (_vm.IsStepGameRouteDiagram && (_vm.IsConnected || _vm.IsRoutesApplied))
                    {
                        _laserStoryboard?.Begin();
                    }
                    else
                    {
                        _laserStoryboard?.Stop();
                    }

                    if (_vm.IsStepAnalyzingRoutes)
                    {
                        _spinStoryboard?.Begin();
                    }
                    else
                    {
                        _spinStoryboard?.Stop();
                    }

                    if (Window.GetWindow(this) is MainWindow mainWindow)
                        mainWindow.UpdateStatusToggle(_vm.IsGlobalOptimizationActive);
                });
            }
        };

        if (_vm.IsStepGameRouteDiagram && (_vm.IsConnected || _vm.IsRoutesApplied))
            _laserStoryboard?.Begin();

        if (_vm.IsStepAnalyzingRoutes)
            _spinStoryboard?.Begin();
    }

    private void BtnBrowseGames_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToLibrary();
        }
    }

    private void ConfiguredGameCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ConfiguredGameItem item)
        {
            _vm.ConfigureGame(item.Game);
            _vm.CurrentFlowStep = ConnectFlowStep.GameRouteDiagram;
        }
    }

    private void BtnRemoveConfiguredGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ConfiguredGameItem item)
        {
            _vm.RemoveConfiguredGame(item);
        }
    }

    private void BtnBackToHome_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentFlowStep = ConnectFlowStep.ConnectionsHome;
    }

    private void BtnResetSettings_Click(object sender, RoutedEventArgs e)
    {
        _vm.ResetGameConfiguration();
    }

    private void BtnOpenSelectRoute_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentFlowStep = ConnectFlowStep.SelectRoute;
    }

    private void BtnBackToGameInitial_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentFlowStep = ConnectFlowStep.GameInitialRoute;
    }

    private void BtnBackToSelectRoute_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentFlowStep = ConnectFlowStep.SelectRoute;
    }

    private void BtnSelectAutoMode_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsAutoServerSelection = true;
        _vm.IsManualServerSelection = false;
    }

    private void BtnSelectManualMode_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.IsManualRelayAllowed)
        {
            ModernPromptWindow.ShowAlert(
                "FEATURE COMING SOON",
                "Manual Relay Selection is currently undergoing maintenance and fine-tuning.\n\nRouteXia is automatically analyzing and routing your traffic through the lowest latency route with Auto Best.",
                ModernPromptType.Information,
                "UNDERSTOOD",
                "Smart Routing");
            return;
        }

        _vm.IsManualServerSelection = true;
        _vm.IsAutoServerSelection = false;
    }

    private async void ServerItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServerNode server)
        {
            _vm.SelectServerNode(server);
            var region = _vm.RegionsList.FirstOrDefault() ?? new RegionItem { Name = server.Region, DisplayName = server.Name };
            await _vm.StartAnalyzingRoutesAsync(region);
        }
    }

    private async void RegionItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RegionItem region)
        {
            await _vm.StartAnalyzingRoutesAsync(region);
        }
    }

    private async void BtnApplyRoutes_Click(object sender, RoutedEventArgs e)
    {
        await _vm.ApplyRoutesAsync();
    }

    private async void BtnReOptimize_Click(object sender, RoutedEventArgs e)
    {
        await _vm.PingAllRelaysAsync();

        var region = _vm.SelectedRegionItem ?? _vm.RegionsList.FirstOrDefault();
        if (region == null && _vm.SelectedServer != null)
        {
            region = new RegionItem
            {
                Name = _vm.SelectedServer.Region,
                DisplayName = _vm.SelectedServer.Name
            };
        }

        if (region != null)
        {
            await _vm.StartAnalyzingRoutesAsync(region);
        }
    }

    private void BtnToggleAdvanced_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsAdvancedSettingsOpen = !_vm.IsAdvancedSettingsOpen;
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.IsConnected)
            await _vm.DisconnectAsync();
        else if (_vm.CanConnect)
            await _vm.ConnectAsync();
    }

    private void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
    {
        var game = _vm.CurrentGame;
        var attemptedLaunch = false;

        if (!string.IsNullOrEmpty(game.LaunchUri))
        {
            try
            {
                attemptedLaunch = true;
                Process.Start(new ProcessStartInfo
                {
                    FileName = game.LaunchUri,
                    UseShellExecute = true
                });
                return;
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(game.FallbackExe))
        {
            try
            {
                attemptedLaunch = true;
                Process.Start(new ProcessStartInfo
                {
                    FileName = game.FallbackExe,
                    UseShellExecute = true
                });
                return;
            }
            catch { }
        }

        ModernPromptWindow.ShowAlert(
            $"LAUNCH {game.ShortName.ToUpperInvariant()}",
            attemptedLaunch
                ? $"{game.ShortName} could not be launched automatically. Please launch it from Steam / your desktop, and RouteXia will keep the boost active."
                : $"Please open {game.ShortName} manually, and RouteXia will optimize your connection.",
            ModernPromptType.Information,
            "GOT IT",
            "Game Launcher");
    }
}

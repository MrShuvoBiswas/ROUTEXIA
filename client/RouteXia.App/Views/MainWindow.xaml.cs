using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;
using Wpf.Ui.Controls;

namespace RouteXia.App.Views;

public partial class MainWindow : FluentWindow
{
    private ConnectView? _connectView;
    private GameLibraryView? _libraryView;
    private ActiveConnectionsView? _connectionsView;
    private NetworkShieldView? _networkView;
    private DiagnosticsView? _diagView;
    private SettingsView? _settingsView;
    private AuthView? _authView;
    private bool _isClosingAnimated;

    private System.Windows.Controls.Button[] _navButtons = null!;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DataContext = App.Services.GetRequiredService<ConnectViewModel>();
        _navButtons = [BtnNavHome, BtnNavLibrary, BtnNavNetwork, BtnNavSettings, BtnNavAccount, BtnNavHelp];
        NavigateToHome();

        // ── Auto-Update Event Subscriptions (Discord/Spotify-Style In-App Updater) ──
        RouteXia.App.Services.UpdateManager.Instance.UpdateDetected += (version) =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtModalUpdateTitle.Text = "NEW UPDATE AVAILABLE";
                TxtModalUpdateVersion.Text = $"Updating to ROUTEXIA v{version}";
                TxtModalUpdateStatus.Text = "Downloading optimized engine package...";
                ModalUpdateProgressBar.Value = 0;
                TxtModalProgressPercent.Text = "0%";
                UpdateCenterModalOverlay.Visibility = Visibility.Visible;

                TxtUpdateToastTitle.Text = $"🚀 New Update (v{version})";
                TxtUpdateToastMsg.Text = "Downloading update package in background...";
            });
        };

        RouteXia.App.Services.UpdateManager.Instance.UpdateDownloadProgress += (percent) =>
        {
            Dispatcher.Invoke(() =>
            {
                ModalUpdateProgressBar.Value = percent;
                TxtModalProgressPercent.Text = $"{(int)percent}%";
                TxtModalUpdateStatus.Text = percent >= 100 
                    ? "Verifying package integrity..." 
                    : $"Downloading update... ({(int)percent}%)";
            });
        };

        RouteXia.App.Services.UpdateManager.Instance.UpdateReadyForRestart += (version, isConnected) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (isConnected)
                {
                    // If connected / gaming, hide center modal and show subtle toast so gaming is NOT interrupted!
                    UpdateCenterModalOverlay.Visibility = Visibility.Collapsed;
                    UpdateToastOverlay.Visibility = Visibility.Visible;
                    TxtUpdateToastTitle.Text = $"🛡️ Update v{version} Downloaded";
                    TxtUpdateToastMsg.Text = "Will auto-apply when you disconnect or exit game.";
                }
                else
                {
                    TxtModalUpdateStatus.Text = "Update downloaded! Restarting application...";
                }
            });
        };

        RouteXia.App.Services.UpdateManager.Instance.RestartCountdown += (sec) =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtModalUpdateTitle.Text = "APPLYING UPDATE";
                TxtModalUpdateStatus.Text = $"Restarting ROUTEXIA in {sec}s...";
                TxtModalProgressPercent.Text = "Restarting...";

                TxtUpdateToastTitle.Text = "🔄 Applying Update...";
                TxtUpdateToastMsg.Text = $"Restarting ROUTEXIA in {sec}s...";
            });
        };

        // ── Auto-transition to LoginWindow on Logout / Session Invalidation ──
        var apiClient = App.Services.GetRequiredService<RouteXia.VpnClient.Api.RouteXiaApiClient>();
        apiClient.AuthStateChanged += () =>
        {
            if (!apiClient.IsAuthenticated)
            {
                Dispatcher.Invoke(() =>
                {
                    if (Application.Current != null && Application.Current.MainWindow == this)
                    {
                        var loginWin = App.Services.GetRequiredService<LoginWindow>();
                        Application.Current.MainWindow = loginWin;
                        loginWin.Show();
                        this.Close();
                    }
                });
            }
        };

        // ── Discord-Style Startup & Background Auto-Update ─────────────────
        // Checks on launch and periodically every 2 minutes.
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(1200);
            await RouteXia.App.Services.UpdateManager.Instance.CheckAndApplyStartupUpdateAsync();
            RouteXia.App.Services.UpdateManager.Instance.StartPeriodicBackgroundCheck(1);
        });
    }

    private void BtnDismissUpdateToast_Click(object sender, RoutedEventArgs e)
    {
        UpdateToastOverlay.Visibility = Visibility.Collapsed;
    }

    private void BtnNavHome_Click(object sender, RoutedEventArgs e)
    {
        NavigateToHome();
    }

    private void BtnNavLibrary_Click(object sender, RoutedEventArgs e)
    {
        NavigateToLibrary();
    }

    private void BtnNavNetwork_Click(object sender, RoutedEventArgs e)
    {
        NavigateToNetwork();
    }

    private void BtnNavAccount_Click(object sender, RoutedEventArgs e)
    {
        NavigateToAccount();
    }

    private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSettings();
    }

    private void BtnNavHelp_Click(object sender, RoutedEventArgs e)
    {
        ModernPromptWindow.ShowAlert(
            "ROUTEXIA HELP & SUPPORT",
            "ROUTEXIA Gaming Multipath Route Optimizer\n\n• Official Support Email: help@routexia.in\n\nFor any inquiries, billing assistance, or technical support, feel free to reach out to us directly.",
            ModernPromptType.Information,
            "CLOSE",
            "Help Center");
    }

    private void BtnSidebarSignIn_Click(object sender, RoutedEventArgs e)
    {
        NavigateToAccount();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void TopBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosingAnimated)
        {
            e.Cancel = true;
            AnimateAndClose();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        AnimateAndClose();
    }

    private async void AnimateAndClose()
    {
        if (_isClosingAnimated) return;
        _isClosingAnimated = true;

        if (ClosingOverlay != null)
        {
            ClosingOverlay.Visibility = Visibility.Visible;

            var overlayFade = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150));
            ClosingOverlay.BeginAnimation(UIElement.OpacityProperty, overlayFade);

            if (ClosingSpinnerTransform != null)
            {
                var spinAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromMilliseconds(800),
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };
                ClosingSpinnerTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spinAnim);
            }
        }

        await System.Threading.Tasks.Task.Delay(450);

        var windowFade = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150));
        windowFade.Completed += (_, _) => App.PerformFullShutdown();
        BeginAnimation(Window.OpacityProperty, windowFade);
    }

    private void SetActiveNav(System.Windows.Controls.Button activeButton)
    {
        foreach (var btn in _navButtons)
        {
            btn.Tag = btn == activeButton ? "active" : null;
        }
    }

    public void NavigateToHome()
    {
        SetActiveNav(BtnNavHome);
        _connectView ??= new ConnectView();
        MainFrame.Navigate(_connectView);
    }

    public void NavigateToNetwork()
    {
        SetActiveNav(BtnNavNetwork);
        _networkView ??= new NetworkShieldView();
        MainFrame.Navigate(_networkView);
    }

    public void NavigateToAccount()
    {
        SetActiveNav(BtnNavAccount);
        _authView ??= new AuthView();
        MainFrame.Navigate(_authView);
    }

    public void NavigateToSettings()
    {
        SetActiveNav(BtnNavSettings);
        _settingsView ??= new SettingsView();
        MainFrame.Navigate(_settingsView);
    }

    public void NavigateToLibrary()
    {
        SetActiveNav(BtnNavLibrary);
        _libraryView ??= new GameLibraryView();
        MainFrame.Navigate(_libraryView);
    }

    public void NavigateToConnections()
    {
        _connectionsView ??= new ActiveConnectionsView();
        MainFrame.Navigate(_connectionsView);
    }

    public void NavigateToDiagnostics()
    {
        _diagView ??= new DiagnosticsView();
        MainFrame.Navigate(_diagView);
    }

    public void NavigateToConnectFromLibrary()
    {
        NavigateToHome();
    }

    public void UpdateStatusToggle(bool isConnected)
    {
    }
}

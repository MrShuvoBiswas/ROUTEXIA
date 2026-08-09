using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RouteXia.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace RouteXia.App.Views;

public partial class ConnectView : Page
{
    private ConnectViewModel _vm = null!;
    private Storyboard? _pulseStoryboard;
    private System.Windows.Threading.DispatcherTimer? _uptimeTimer;

    private bool _isInitialized;

    public ConnectView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetRequiredService<ConnectViewModel>();
        DataContext = _vm;

        if (!_isInitialized)
        {
            // Wire events once
            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ConnectViewModel.State))
                    Dispatcher.Invoke(() => UpdateUIForState(_vm.State));
            };

            _vm.LogMessage += OnLogMessage;

            // Get the pulse storyboard from resources
            _pulseStoryboard = Resources.Contains("PulseAnimation")
                ? (Storyboard)Resources["PulseAnimation"]
                : null;

            _isInitialized = true;
        }

        UpdateUIForState(_vm.State);
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.IsConnected)
            await _vm.DisconnectAsync();
        else if (_vm.CanConnect)
            await _vm.ConnectAsync();
    }

    private void UpdateUIForState(ConnectionState state)
    {
        switch (state)
        {
            case ConnectionState.Connecting:
                TxtStatus.Text = "CONNECTING...";
                TxtStatus.Foreground = FindResource("AccentBrush") as Brush;
                IconPower.Visibility = Visibility.Collapsed;
                LoadingRing.Visibility = Visibility.Visible;
                BtnConnect.IsEnabled = false;
                StopPulse();
                break;

            case ConnectionState.Connected:
                TxtStatus.Text = "CONNECTED";
                TxtStatus.Foreground = (Brush)FindResource("StatusGoodBrush");
                IconPower.Foreground = (Brush)FindResource("StatusGoodBrush");
                IconPower.Visibility = Visibility.Visible;
                LoadingRing.Visibility = Visibility.Collapsed;
                BtnConnect.IsEnabled = true;
                StartPulse();
                StartUptimeTimer();
                StatusDot.Fill = (Brush)FindResource("StatusGoodBrush");
                TxtStatusSmall.Text = "Connected";
                ImprovementBadge.Visibility = Visibility.Visible;
                break;

            case ConnectionState.Disconnected:
                TxtStatus.Text = "DISCONNECTED";
                TxtStatus.Foreground = (Brush)FindResource("TextMutedBrush");
                IconPower.Foreground = (Brush)FindResource("AccentBrush");
                IconPower.Visibility = Visibility.Visible;
                LoadingRing.Visibility = Visibility.Collapsed;
                BtnConnect.IsEnabled = true;
                StopPulse();
                StopUptimeTimer();
                StatusDot.Fill = (Brush)FindResource("StatusBadBrush");
                TxtStatusSmall.Text = "Disconnected";
                TxtRelayPingLarge.Text = "--";
                TxtUptime.Text = "--:--:--";
                ImprovementBadge.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void OnLogMessage(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtLog.Text = message + "\n" + TxtLog.Text;
            // Keep log short — last 20 lines
            var lines = TxtLog.Text.Split('\n');
            if (lines.Length > 20)
                TxtLog.Text = string.Join("\n", lines.Take(20));
        });
    }

    // ── Pulse animation ───────────────────────────────────────────────────────
    private void StartPulse()
    {
        Ring1.Visibility = Visibility.Visible;
        Ring2.Visibility = Visibility.Visible;
        Ring3.Visibility = Visibility.Visible;
        _pulseStoryboard?.Begin();
    }

    private void StopPulse()
    {
        _pulseStoryboard?.Stop();
        Ring1.Visibility = Visibility.Hidden;
        Ring2.Visibility = Visibility.Hidden;
        Ring3.Visibility = Visibility.Hidden;
    }

    // ── Uptime timer ──────────────────────────────────────────────────────────
    private void StartUptimeTimer()
    {
        _uptimeTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uptimeTimer.Tick += (s, e) =>
        {
            var uptime = _vm.Uptime;
            TxtUptime.Text = $"{(int)uptime.TotalHours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";

            // Update ping display from multipath stats
            if (_vm.BestPingMs > 0 && _vm.BestPingMs < 9999)
            {
                TxtRelayPing.Text = $"{_vm.BestPingMs:F0}ms";
                TxtRelayPingLarge.Text = $"{_vm.BestPingMs:F0}";
            }
            else
            {
                TxtRelayPing.Text = "--ms";
                TxtRelayPingLarge.Text = "--";
            }
            ImprovementBadge.Visibility = _vm.HasImprovement ? Visibility.Visible : Visibility.Collapsed;
        };
        _uptimeTimer.Start();
    }

    private void StopUptimeTimer()
    {
        _uptimeTimer?.Stop();
        _uptimeTimer = null;
    }
}

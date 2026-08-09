using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace RouteXia.App.Views;

public partial class MainWindow : FluentWindow
{
    private ConnectView? _connectView;
    private SettingsView? _settingsView;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigateToConnect();
    }

    private void BtnNavConnect_Click(object sender, RoutedEventArgs e)
    {
        NavigateToConnect();
        BtnNavConnect.Tag = "active";
        BtnNavSettings.Tag = null;
    }

    private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSettings();
        BtnNavSettings.Tag = "active";
        BtnNavConnect.Tag = null;
    }

    private void NavigateToConnect()
    {
        _connectView ??= new ConnectView();
        MainFrame.Navigate(_connectView);
    }

    private void NavigateToSettings()
    {
        _settingsView ??= new SettingsView();
        MainFrame.Navigate(_settingsView);
    }
}

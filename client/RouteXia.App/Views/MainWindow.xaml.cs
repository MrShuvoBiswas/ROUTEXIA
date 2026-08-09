using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace RouteXia.App.Views;

public partial class MainWindow : FluentWindow
{
    private ConnectView? _connectView;
    private SettingsView? _settingsView;
    private AuthView? _authView;

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
        BtnNavAccount.Tag = null;
        BtnNavSettings.Tag = null;
    }

    private void BtnNavAccount_Click(object sender, RoutedEventArgs e)
    {
        NavigateToAccount();
        BtnNavAccount.Tag = "active";
        BtnNavConnect.Tag = null;
        BtnNavSettings.Tag = null;
    }

    private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSettings();
        BtnNavSettings.Tag = "active";
        BtnNavConnect.Tag = null;
        BtnNavAccount.Tag = null;
    }

    private void NavigateToConnect()
    {
        _connectView ??= new ConnectView();
        MainFrame.Navigate(_connectView);
    }

    private void NavigateToAccount()
    {
        _authView ??= new AuthView();
        MainFrame.Navigate(_authView);
    }

    private void NavigateToSettings()
    {
        _settingsView ??= new SettingsView();
        MainFrame.Navigate(_settingsView);
    }
}

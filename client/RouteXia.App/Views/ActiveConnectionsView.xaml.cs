using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class ActiveConnectionsView : Page
{
    private ConnectViewModel _vm = null!;

    public ActiveConnectionsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetRequiredService<ConnectViewModel>();
        DataContext = _vm;
    }

    private void BtnBrowseGames_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToLibrary();
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
        {
            await _vm.LoadDynamicServersAsync();
        }
    }

    private void BtnOpenOptimize_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToHome();
        }
    }
}

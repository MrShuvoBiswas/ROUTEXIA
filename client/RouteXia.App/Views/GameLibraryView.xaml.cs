using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.Data;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class GameLibraryView : Page
{
    private GameLibraryViewModel _vm = null!;

    public GameLibraryView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetRequiredService<GameLibraryViewModel>();
        DataContext = _vm;
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string category)
        {
            _vm.SelectedCategory = category;
        }
    }

    private void GameCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GameDefinition game)
        {
            if (!game.IsSupported)
            {
                // Show coming soon message
                ModernPromptWindow.ShowAlert(
                    $"{game.Name.ToUpperInvariant()} — COMING SOON",
                    $"We're actively working on adding routing optimization and dedicated low-ping relay routes for {game.Name}.",
                    ModernPromptType.Information,
                    "GOT IT",
                    "Game Library");
                return;
            }

            // Navigate to the connect view with this game selected
            // Find MainWindow and navigate
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                // Set the selected game on the ConnectViewModel
                var connectVm = App.Services.GetRequiredService<ConnectViewModel>();
                connectVm.ConfigureGame(game);

                // Navigate to connect view
                mainWindow.NavigateToConnectFromLibrary();
            }
        }
    }
}

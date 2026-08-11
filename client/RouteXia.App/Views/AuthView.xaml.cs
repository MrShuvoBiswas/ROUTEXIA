using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class AuthView : Page
{
    private AuthViewModel _vm = null!;

    public AuthView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetRequiredService<AuthViewModel>();
        DataContext = _vm;
    }

    private void BtnMoreDetails_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://routexia.com/dashboard/profile",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void BtnActivateAffiliate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://routexia.com/affiliate/activate",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void BtnSubscribe_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://routexia.com/pricing",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        _vm.Logout();

        // Open LoginWindow and close MainWindow
        var loginWindow = App.Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();

        var currentWindow = Window.GetWindow(this);
        currentWindow?.Close();
    }
}

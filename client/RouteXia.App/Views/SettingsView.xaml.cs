using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class SettingsView : Page
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<SettingsViewModel>();
    }
}

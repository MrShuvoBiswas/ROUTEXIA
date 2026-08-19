using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class SpeedTestView : Page
{
    private SpeedTestViewModel _vm = null!;

    public SpeedTestView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            _vm = App.Services.GetRequiredService<SpeedTestViewModel>();
            DataContext = _vm;
            await _vm.RunBenchmarkAsync();
        };
    }

    private async void BtnRunTest_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
        {
            await _vm.RunBenchmarkAsync();
        }
    }
}

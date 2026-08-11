using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class DiagnosticsView : Page
{
    private DiagnosticsViewModel _vm = null!;

    public DiagnosticsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _vm = App.Services.GetRequiredService<DiagnosticsViewModel>();
            DataContext = _vm;
        };
    }

    private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
            await _vm.FlushDnsAsync();
    }

    private async void BtnResetWinsock_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
            await _vm.ResetWinsockAsync();
    }

    private async void BtnTestPorts_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
            await _vm.TestGamePortsAsync();
    }
}

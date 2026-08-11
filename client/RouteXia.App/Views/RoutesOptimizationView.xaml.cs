using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class RoutesOptimizationView : Page
{
    private RoutesViewModel _vm = null!;

    public RoutesOptimizationView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _vm = App.Services.GetRequiredService<RoutesViewModel>();
            DataContext = _vm;
        };
    }
}

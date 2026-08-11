using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class NetworkShieldView : Page
{
    private NetworkShieldViewModel _vm = null!;

    public NetworkShieldView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _vm = App.Services.GetRequiredService<NetworkShieldViewModel>();
            DataContext = _vm;
        };
    }
}

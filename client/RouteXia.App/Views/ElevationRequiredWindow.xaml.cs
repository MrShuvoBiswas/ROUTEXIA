using System.Windows;
using RouteXia.VpnClient.Interception;

namespace RouteXia.App.Views;

public partial class ElevationRequiredWindow : Window
{
    public ElevationRequiredWindow(string? diagnosticDetail = null)
    {
        InitializeComponent();
        if (!string.IsNullOrEmpty(diagnosticDetail))
        {
            DiagnosticTextBlock.Text = diagnosticDetail;
        }
    }

    private void BtnRelaunch_Click(object sender, RoutedEventArgs e)
    {
        bool success = DriverHealthChecker.RelaunchElevated();
        if (success)
        {
            App.PerformFullShutdown();
        }
        else
        {
            MessageBox.Show(
                "Failed to request elevation. Please manually right-click RouteXia and select 'Run as administrator'.",
                "Elevation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        App.PerformFullShutdown();
    }
}

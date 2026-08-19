using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using RouteXia.VpnClient.Interception;

namespace RouteXia.App.Views;

public partial class CrashReportWindow : Window
{
    private readonly Exception _exception;
    private readonly string? _logFilePath;

    public CrashReportWindow(Exception ex, string? logFilePath = null)
    {
        InitializeComponent();
        _exception = ex;
        _logFilePath = logFilePath;

        ExceptionMessageTextBlock.Text = $"[{ex.GetType().Name}] {ex.Message}";
        StackTraceTextBox.Text = ex.ToString();

        if (!string.IsNullOrEmpty(_logFilePath))
        {
            LogPathTextBlock.Text = $"Detailed report written to: {_logFilePath}";
        }
    }

    private void BtnCopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string contentToCopy;
            if (!string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
            {
                contentToCopy = File.ReadAllText(_logFilePath);
            }
            else
            {
                contentToCopy = $"[{_exception.GetType().FullName}]\nMessage: {_exception.Message}\n\nStackTrace:\n{_exception.StackTrace}";
            }

            Clipboard.SetText(contentToCopy);
            MessageBox.Show(
                "Diagnostic details copied to clipboard!",
                "Copied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception copyEx)
        {
            MessageBox.Show($"Failed to copy to clipboard: {copyEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RouteXia", "Logs");
            Directory.CreateDirectory(logsDir);
            Process.Start(new ProcessStartInfo
            {
                FileName = logsDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open logs folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnRestart_Click(object sender, RoutedEventArgs e)
    {
        DriverHealthChecker.RelaunchElevated();
        App.PerformFullShutdown();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

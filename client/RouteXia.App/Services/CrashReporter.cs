using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using RouteXia.App.Views;
using RouteXia.VpnClient.Interception;

namespace RouteXia.App.Services;

public class CrashReportPayload
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string AppVersion { get; set; } = "0.1.0-beta";
    public string OsVersion { get; set; } = RuntimeInformation.OSDescription;
    public string Architecture { get; set; } = RuntimeInformation.ProcessArchitecture.ToString();
    public string DotNetVersion { get; set; } = RuntimeInformation.FrameworkDescription;
    public bool IsAdmin { get; set; }
    public string DriverStatus { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string ExceptionMessage { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public List<string> InnerExceptions { get; set; } = [];
    public List<NetworkAdapterSummary> NetworkAdapters { get; set; } = [];
}

public class NetworkAdapterSummary
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InterfaceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long SpeedMbps { get; set; }
}

public static class CrashReporter
{
    private static bool _isHandlingCrash;

    public static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                HandleException(ex, "AppDomain.UnhandledException", isFatal: true);
            }
        };

        Application.Current.DispatcherUnhandledException += (s, args) =>
        {
            HandleException(args.Exception, "Dispatcher.UnhandledException", isFatal: false);
            args.Handled = true;
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            args.SetObserved();
            try
            {
                GenerateCrashReport(args.Exception, "TaskScheduler.UnobservedTaskException");
            }
            catch { }
        };
    }

    public static string GenerateCrashReport(Exception ex, string context)
    {
        var payload = new CrashReportPayload
        {
            IsAdmin = DriverHealthChecker.IsRunningAsAdmin(),
            ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
            ExceptionMessage = ex.Message,
            StackTrace = ex.StackTrace ?? "No stack trace available"
        };

        DriverHealthChecker.CheckDriverHealth(out var diag);
        payload.DriverStatus = diag;

        var currentEx = ex.InnerException;
        while (currentEx != null)
        {
            payload.InnerExceptions.Add($"[{currentEx.GetType().Name}] {currentEx.Message}\n{currentEx.StackTrace}");
            currentEx = currentEx.InnerException;
        }

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
            {
                payload.NetworkAdapters.Add(new NetworkAdapterSummary
                {
                    Name = nic.Name,
                    Description = nic.Description,
                    InterfaceType = nic.NetworkInterfaceType.ToString(),
                    Status = nic.OperationalStatus.ToString(),
                    SpeedMbps = nic.Speed > 0 ? nic.Speed / 1_000_000 : 0
                });
            }
        }
        catch { /* Network discovery gracefully handled */ }

        string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RouteXia", "Logs");
        Directory.CreateDirectory(logsDir);

        string fileName = $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(logsDir, fileName);

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(payload, options);
        File.WriteAllText(filePath, json);

        return filePath;
    }

    public static void HandleException(Exception ex, string context, bool isFatal)
    {
        if (_isHandlingCrash) return;
        _isHandlingCrash = true;

        try
        {
            string logPath = GenerateCrashReport(ex, context);

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var crashWindow = new CrashReportWindow(ex, logPath);
                crashWindow.ShowDialog();
            });
        }
        catch
        {
            // Fallback if WPF dispatch fails
            MessageBox.Show(
                $"RouteXia Fatal Crash:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "RouteXia Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            if (isFatal)
            {
                App.PerformFullShutdown();
            }
            else
            {
                _isHandlingCrash = false;
            }
        }
    }
}

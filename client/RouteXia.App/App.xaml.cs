using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;
using RouteXia.App.Views;
using RouteXia.VpnClient.Routing;
using RouteXia.VpnClient.KillSwitch;
using RouteXia.VpnClient.Interception;
using RouteXia.VpnClient.Api;
using System.Security.Principal;
using System;

namespace RouteXia.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handling to log and show crashes
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Fatal error: {ex.Message}\n\nStack:\n{ex.StackTrace}", "RouteXia Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Application Error: {args.Exception.Message}\n\nStack:\n{args.Exception.StackTrace}", "RouteXia Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Check admin privileges — required for WinDivert + kill-switch firewall rules
        if (!IsRunningAsAdmin())
        {
            MessageBox.Show(
                "RouteXia requires Administrator privileges to intercept PUBG network traffic.\n\n" +
                "WinDivert (the packet interception engine) requires admin rights to load its kernel filter driver.\n\n" +
                "Please right-click the app and select 'Run as administrator'.",
                "Administrator Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }

        // Ensure WPF does not auto-shutdown when switching between LoginWindow and MainWindow
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var apiClient = Services.GetRequiredService<RouteXiaApiClient>();

        // ExitLag style: If authenticated, open Dashboard directly; otherwise show LoginWindow
        if (apiClient.IsAuthenticated)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        else
        {
            var loginWindow = Services.GetRequiredService<LoginWindow>();
            MainWindow = loginWindow;
            loginWindow.Show();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Backend API Client (Handles Auth, dynamic relays, trial/subscription) ──
        services.AddSingleton<RouteXiaApiClient>();

        // ── Relay endpoints (Default fallback Singapore) ───────────────────────
        var defaultRelays = new[]
        {
            new RelayEndpoint("3.1.31.201", 9001, "SG"),   // Singapore AWS EC2 VPS
        };

        // ── Core routing + interception services ───────────────────────────────
        services.AddSingleton(_ => new MultipathRouter(defaultRelays));
        services.AddSingleton<KillSwitchManager>();

        // WinDivert packet interceptor — the real network layer
        services.AddSingleton<WinDivertInterceptor>();

        // PUBG game server tracker (populated from captured packet destinations)
        services.AddSingleton<PubgServerTracker>();

        // ── ViewModels ─────────────────────────────────────────────────────────
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AuthViewModel>();
        services.AddSingleton<ConnectViewModel>();
        services.AddSingleton<GameLibraryViewModel>();
        services.AddSingleton<RoutesViewModel>();
        services.AddSingleton<NetworkShieldViewModel>();
        services.AddSingleton<SpeedTestViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();

        // ── Views ──────────────────────────────────────────────────────────────
        services.AddTransient<LoginWindow>();
        services.AddSingleton<MainWindow>();
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void PerformFullShutdown()
    {
        try
        {
            // 1. Stop WinDivert interceptor & release native kernel driver handle
            var interceptor = Services?.GetService<WinDivertInterceptor>();
            interceptor?.Dispose();

            // 2. Emergency cleanup on kill switch (remove any netsh firewall rules)
            var killSwitch = Services?.GetService<KillSwitchManager>();
            killSwitch?.EmergencyCleanup();
            killSwitch?.Dispose();

            // 3. Dispose multipath router (close UDP sockets & metrics timers)
            var router = Services?.GetService<MultipathRouter>();
            router?.Dispose();
        }
        catch { /* best effort on exit */ }

        try
        {
            if (Current != null)
            {
                Current.Shutdown();
            }
        }
        catch { }

        // Guarantee 100% process termination from Task Manager without residual background threads
        Environment.Exit(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PerformFullShutdown();
        base.OnExit(e);
    }
}

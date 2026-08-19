using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;
using RouteXia.App.Views;
using RouteXia.VpnClient.Routing;
using RouteXia.VpnClient.KillSwitch;
using RouteXia.VpnClient.Interception;
using RouteXia.VpnClient.Api;
using RouteXia.App.Services;
using System.Security.Principal;
using System;

namespace RouteXia.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // ── 0. Velopack Lifecycle Hooks (Required for seamless background updates) ─
        try
        {
            Velopack.VelopackApp.Build().Run();
        }
        catch { }

        base.OnStartup(e);

        // ── Structured Global Crash Reporting Engine (T005, T006, T007) ──────
        CrashReporter.Initialize();

        // ── Pre-flight Driver & UAC Elevation Health Check (T001, T002, T003) ─
        var health = DriverHealthChecker.CheckDriverHealth(out string diag);
        if (health != DriverHealthStatus.Healthy)
        {
            var elevationWindow = new ElevationRequiredWindow(diag);
            elevationWindow.ShowDialog();
            return;
        }

        // Ensure WPF does not auto-shutdown when switching between LoginWindow and MainWindow
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var apiClient = Services.GetRequiredService<RouteXiaApiClient>();

        // ExitLag style: If authenticated with active/saved session, open Dashboard directly without flashing LoginWindow
        if (apiClient.IsAuthenticated || apiClient.HasSavedToken)
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

        // ── Core routing + interception services (Initialized dynamically via API) ───
        services.AddSingleton(sp =>
        {
            var apiClient = sp.GetRequiredService<RouteXiaApiClient>();
            var relays = apiClient.ActiveRelays
                .Select(r => new RelayEndpoint(r.Host, (ushort)r.Port, r.RegionCode ?? "SG"))
                .ToArray();
            return new MultipathRouter(relays);
        });
        services.AddSingleton<KillSwitchManager>();

        // System Network, MTU 1393 & Cloudflare Gaming DNS Optimizer
        services.AddSingleton<RouteXia.VpnClient.Optimization.SystemNetworkOptimizer>();

        // WinDivert packet interceptor — the real network layer
        services.AddSingleton<WinDivertInterceptor>();

        // PUBG game server tracker (populated from captured packet destinations)
        services.AddSingleton<PubgServerTracker>();

        // ── Auto-Update Manager ────────────────────────────────────────────────
        services.AddSingleton(UpdateManager.Instance);

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

            // 3. Restore default network adapter MTU and DNS settings
            var networkOpt = Services?.GetService<RouteXia.VpnClient.Optimization.SystemNetworkOptimizer>();
            networkOpt?.RestoreDefaultNetworkSettings();
            networkOpt?.Dispose();

            // 4. Dispose multipath router (close UDP sockets & metrics timers)
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

using System;
using Velopack;

namespace RouteXia.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // ── Velopack Application Lifecycle Hook ──────────────────────────
            // Must be called at the very beginning of startup.
            // Handles install, update, restart, hooks, and desktop shortcuts.
            VelopackApp.Build().Run();

            // ── Launch WPF Application ───────────────────────────────────────
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Fatal Startup Error] {ex}");
        }
    }
}

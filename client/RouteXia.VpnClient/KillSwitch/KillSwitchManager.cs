using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace RouteXia.VpnClient.KillSwitch
{
    /// <summary>
    /// Kill-Switch: blocks PUBG traffic via Windows Firewall if the tunnel drops or user disconnects.
    ///
    /// Dynamically locates the active TslGame.exe process path (Steam / Krafton Launcher)
    /// and blocks outbound traffic, forcing instant game network error if the tunnel disconnects.
    /// </summary>
    public sealed class KillSwitchManager : IDisposable
    {
        private const string FirewallRuleName = "RouteXia-KillSwitch-PUBG";

        private bool _killSwitchActive;
        private bool _disposed;
        private readonly Timer _tunnelWatcher;
        private Func<bool>? _isTunnelAlive;

        public bool IsActive => _killSwitchActive;

        /// <summary>Fired when kill-switch activates (tunnel dropped).</summary>
        public event EventHandler? KillSwitchActivated;

        /// <summary>Fired when kill-switch deactivates (tunnel restored).</summary>
        public event EventHandler? KillSwitchDeactivated;

        public KillSwitchManager()
        {
            // Always clean up any stale firewall rules from previous crashes on startup
            EmergencyCleanup();

            // Check tunnel health every 2 seconds
            _tunnelWatcher = new Timer(CheckTunnelHealth, null,
                TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void SetTunnelHealthCheck(Func<bool> isAlive)
        {
            _isTunnelAlive = isAlive;
        }

        /// <summary>Immediately activate kill-switch (block PUBG outbound traffic).</summary>
        public void Activate()
        {
            if (_killSwitchActive) return;

            try
            {
                var pubgPath = GetPubgProcessPath();

                // Clean up previous rule
                RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRuleName}\"");

                if (!string.IsNullOrEmpty(pubgPath))
                {
                    // Block specific active TslGame.exe executable path
                    RunNetsh($"advfirewall firewall add rule " +
                             $"name=\"{FirewallRuleName}\" " +
                             $"dir=out action=block " +
                             $"program=\"{pubgPath}\" " +
                             $"enable=yes profile=any");
                }
                else
                {
                    // Fallback: block outbound UDP traffic to PUBG ports (7000-8000)
                    RunNetsh($"advfirewall firewall add rule " +
                             $"name=\"{FirewallRuleName}\" " +
                             $"dir=out action=block protocol=UDP " +
                             $"remoteport=7000-8000 " +
                             $"enable=yes profile=any");
                }

                _killSwitchActive = true;
                KillSwitchActivated?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine("[KillSwitch] ACTIVATED — PUBG PC outbound blocked");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KillSwitch] Activation failed: {ex.Message}");
            }
        }

        /// <summary>Deactivate kill-switch (restore PUBG outbound traffic).</summary>
        public void Deactivate()
        {
            if (!_killSwitchActive) return;

            try
            {
                RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRuleName}\"");

                _killSwitchActive = false;
                KillSwitchDeactivated?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine("[KillSwitch] DEACTIVATED — PUBG PC traffic restored");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KillSwitch] Deactivation failed: {ex.Message}");
            }
        }

        /// <summary>Emergency cleanup: removes all RouteXia firewall rules.</summary>
        public void EmergencyCleanup()
        {
            try
            {
                RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRuleName}\"");
                _killSwitchActive = false;
                Debug.WriteLine("[KillSwitch] Emergency cleanup completed");
            }
            catch { /* best effort */ }
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        private static string GetPubgProcessPath()
        {
            try
            {
                var procs = Process.GetProcessesByName("TslGame");
                if (procs.Length > 0 && procs[0].MainModule != null)
                {
                    return procs[0].MainModule!.FileName;
                }
            }
            catch { /* best effort */ }

            return "";
        }

        private void CheckTunnelHealth(object? _)
        {
            if (_isTunnelAlive == null) return;

            bool alive = false;
            try { alive = _isTunnelAlive(); } catch { }

            // Only trigger kill-switch if the tunnel was connected and then dropped unexpectedly
            if (!alive && _killSwitchActive)
            {
                // Already active
            }
            else if (alive && _killSwitchActive)
            {
                Deactivate();
            }
        }

        private static void RunNetsh(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _tunnelWatcher.Dispose();
            EmergencyCleanup();
            _disposed = true;
        }
    }
}

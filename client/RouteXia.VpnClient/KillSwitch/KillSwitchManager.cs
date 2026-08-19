using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using RouteXia.VpnClient.Profiles;

namespace RouteXia.VpnClient.KillSwitch
{
    /// <summary>
    /// Kill-Switch: blocks game traffic via Windows Firewall if the tunnel drops or user disconnects.
    /// Dynamically locates the active game executable path and blocks outbound traffic,
    /// ensuring ZERO leakage without affecting Discord, browsers, or other system traffic.
    /// </summary>
    public sealed class KillSwitchManager : IDisposable
    {
        private const string FirewallRuleName = "RouteXia-KillSwitch";

        private bool _killSwitchActive;
        private bool _disposed;
        private readonly Timer _tunnelWatcher;
        private Func<bool>? _isTunnelAlive;
        private IGameProfile _activeProfile = new PubgGameProfile();

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

        public void SetActiveProfile(IGameProfile profile)
        {
            _activeProfile = profile ?? new PubgGameProfile();
        }

        public void SetTunnelHealthCheck(Func<bool> isAlive)
        {
            _isTunnelAlive = isAlive;
        }

        /// <summary>Immediately activate kill-switch (block game outbound traffic).</summary>
        public void Activate()
        {
            if (_killSwitchActive) return;

            try
            {
                var gamePath = GetGameProcessPath();

                // Clean up previous rule
                RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRuleName}\"");

                if (!string.IsNullOrEmpty(gamePath))
                {
                    // Block specific active game executable path
                    RunNetsh($"advfirewall firewall add rule " +
                             $"name=\"{FirewallRuleName}\" " +
                             $"dir=out action=block " +
                             $"program=\"{gamePath}\" " +
                             $"enable=yes profile=any");
                }
                else
                {
                    // Fallback: block outbound UDP traffic to game's port range
                    RunNetsh($"advfirewall firewall add rule " +
                             $"name=\"{FirewallRuleName}\" " +
                             $"dir=out action=block protocol=UDP " +
                             $"remoteport=7000-20000 " +
                             $"enable=yes profile=any");
                }

                _killSwitchActive = true;
                KillSwitchActivated?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine($"[KillSwitch] ACTIVATED — {_activeProfile.DisplayName} outbound blocked");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KillSwitch] Activation failed: {ex.Message}");
            }
        }

        /// <summary>Deactivate kill-switch (restore game outbound traffic).</summary>
        public void Deactivate()
        {
            if (!_killSwitchActive) return;

            try
            {
                RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRuleName}\"");

                _killSwitchActive = false;
                KillSwitchDeactivated?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine($"[KillSwitch] DEACTIVATED — {_activeProfile.DisplayName} traffic restored");
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
                RunNetsh($"advfirewall firewall delete rule name=\"RouteXia-KillSwitch-PUBG\"");
                _killSwitchActive = false;
                Debug.WriteLine("[KillSwitch] Emergency cleanup completed");
            }
            catch { /* best effort */ }
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        private string GetGameProcessPath()
        {
            try
            {
                var names = _activeProfile.ProcessNames;
                foreach (var name in names)
                {
                    var procs = Process.GetProcessesByName(name);
                    if (procs.Length > 0 && procs[0].MainModule != null)
                    {
                        string path = procs[0].MainModule!.FileName;
                        foreach (var p in procs) p.Dispose();
                        return path;
                    }
                }
            }
            catch { /* best effort */ }

            return "";
        }

        private void CheckTunnelHealth(object? _)
        {
            if (_isTunnelAlive == null) return;

            bool alive = _isTunnelAlive();
            if (!alive && !_killSwitchActive)
            {
                Activate();
            }
            else if (alive && _killSwitchActive)
            {
                Deactivate();
            }
        }

        private static void RunNetsh(string args)
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh.exe",
                        Arguments = args,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                };
                p.Start();
                p.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KillSwitch] Netsh error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _tunnelWatcher.Dispose();
            EmergencyCleanup();
        }
    }
}

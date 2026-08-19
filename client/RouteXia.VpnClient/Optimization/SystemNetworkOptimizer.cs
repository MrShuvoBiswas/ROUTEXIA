using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RouteXia.VpnClient.Optimization
{
    /// <summary>
    /// System-Level Network Adapter, MTU & DNS Optimizer for Low-Latency PC Gaming.
    ///
    /// Applied on Connect:
    ///   1. Sets active adapter MTU to 1393 (eliminates IP packet fragmentation on fiber/broadband).
    ///   2. Configures Cloudflare Ultra-Low Latency Gaming DNS (1.1.1.1 & 1.0.0.1).
    ///   3. Flushes the Windows DNS resolver cache to drop stale/slow ISP DNS routes.
    ///
    /// Restored on Disconnect / Exit:
    ///   Reverts MTU back to standard 1500 and restores default DHCP/DNS settings.
    /// </summary>
    public sealed class SystemNetworkOptimizer : IDisposable
    {
        private string? _optimizedInterfaceName;
        private bool _isOptimized;
        private readonly object _lock = new();

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern int DnsFlushResolverCache();

        public bool IsOptimized => _isOptimized;
        public string? ActiveInterface => _optimizedInterfaceName;

        /// <summary>
        /// Detects the active physical/Wi-Fi adapter providing default internet gateway.
        /// </summary>
        public static string? GetPrimaryActiveInterfaceName()
        {
            try
            {
                var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                  ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    .Where(ni => ni.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                    .OrderByDescending(ni => ni.Speed)
                    .FirstOrDefault();

                return activeInterface?.Name;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Applies MTU 1393, Cloudflare Gaming DNS (1.1.1.1 & 1.0.0.1) and flushes DNS cache.
        /// </summary>
        public async Task<bool> ApplyGamingOptimizationsAsync(int targetMtu = 1393)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        string? iface = GetPrimaryActiveInterfaceName();
                        if (string.IsNullOrEmpty(iface))
                        {
                            // Fallback to Ethernet
                            iface = "Ethernet";
                        }

                        _optimizedInterfaceName = iface;

                        // 1. Configure Cloudflare Ultra-Low Latency Gaming DNS (1.1.1.1 & 1.0.0.1)
                        RunNetsh($"interface ipv4 set dnsservers name=\"{iface}\" source=static address=1.1.1.1 register=none validate=no");
                        RunNetsh($"interface ipv4 add dnsservers name=\"{iface}\" address=1.0.0.1 index=2 validate=no");

                        // 2. Set MTU Clamping to 1393 (or custom target) to prevent IP fragmentation
                        RunNetsh($"interface ipv4 set subinterface \"{iface}\" mtu={targetMtu} store=temporary");
                        RunNetsh($"interface ipv4 set subinterface \"{iface}\" mtu={targetMtu} store=persistent");

                        // 3. Flush Windows DNS cache
                        try { DnsFlushResolverCache(); } catch { }
                        RunProcess("ipconfig", "/flushdns");

                        _isOptimized = true;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SystemNetworkOptimizer] Failed to apply network optimizations: {ex.Message}");
                        return false;
                    }
                }
            });
        }

        /// <summary>
        /// Restores standard DHCP DNS and 1500 MTU on the adapter.
        /// </summary>
        public void RestoreDefaultNetworkSettings()
        {
            lock (_lock)
            {
                if (!_isOptimized && string.IsNullOrEmpty(_optimizedInterfaceName))
                    return;

                try
                {
                    string iface = _optimizedInterfaceName ?? GetPrimaryActiveInterfaceName() ?? "Ethernet";

                    // 1. Restore DNS to DHCP
                    RunNetsh($"interface ipv4 set dnsservers name=\"{iface}\" source=dhcp");

                    // 2. Revert MTU to default 1500
                    RunNetsh($"interface ipv4 set subinterface \"{iface}\" mtu=1500 store=temporary");
                    RunNetsh($"interface ipv4 set subinterface \"{iface}\" mtu=1500 store=persistent");

                    // 3. Flush DNS cache
                    try { DnsFlushResolverCache(); } catch { }
                    RunProcess("ipconfig", "/flushdns");

                    _isOptimized = false;
                    _optimizedInterfaceName = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SystemNetworkOptimizer] Failed to restore default network settings: {ex.Message}");
                }
            }
        }

        private static void RunNetsh(string args)
        {
            RunProcess("netsh", args);
        }

        private static void RunProcess(string exe, string args)
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                p.Start();
                p.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemNetworkOptimizer] Process execution error ({exe} {args}): {ex.Message}");
            }
        }

        public void Dispose()
        {
            RestoreDefaultNetworkSettings();
        }
    }
}

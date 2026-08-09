using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace RouteXia.VpnClient.Interception
{
    /// <summary>
    /// Tracks discovered PUBG game server IPs and ports from actual intercepted packets.
    ///
    /// Unlike the old approach (GetActiveUdpListeners — which only showed local sockets),
    /// this class receives real destination IPs/ports from WinDivertInterceptor,
    /// giving accurate "Match Server IP" display in the UI.
    /// </summary>
    public sealed class PubgServerTracker
    {
        // ── Observed servers ──────────────────────────────────────────────────────
        // Key: "ip:port" string, Value: last seen timestamp
        private readonly ConcurrentDictionary<string, DateTime> _servers = new();

        // ── Current primary server ────────────────────────────────────────────────
        private volatile string _primaryServerDisplay = "--";
        private volatile bool   _isMatchActive;
        private IPAddress?      _primaryIp;
        private ushort          _primaryPort;

        // Timeout: if no packets seen from a server for 15s, consider match ended
        private static readonly TimeSpan ServerTimeout = TimeSpan.FromSeconds(15);

        private readonly Timer _cleanupTimer;

        // ── Events ────────────────────────────────────────────────────────────────
        public event Action<string, bool>? MatchStateChanged; // (serverDisplay, isActive)

        public string PrimaryServerDisplay => _primaryServerDisplay;
        public bool   IsMatchActive        => _isMatchActive;
        public IPAddress? PrimaryIp        => _primaryIp;
        public ushort     PrimaryPort      => _primaryPort;

        public PubgServerTracker()
        {
            // Clean up stale servers every 5 seconds
            _cleanupTimer = new Timer(Cleanup, null,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by WinDivertInterceptor every time a PUBG packet is captured.
        /// Records the destination server and updates match state.
        /// </summary>
        public void OnPacketObserved(IPAddress destIp, ushort destPort)
        {
            string key = $"{destIp}:{destPort}";
            bool isNew = !_servers.ContainsKey(key);

            _servers[key] = DateTime.UtcNow;

            // First time we see this server → update primary
            if (isNew || !_isMatchActive)
            {
                _primaryIp   = destIp;
                _primaryPort = destPort;
                string display = BuildDisplayName(destIp, destPort);

                bool wasActive = _isMatchActive;
                _primaryServerDisplay = display;
                _isMatchActive = true;

                if (!wasActive)
                {
                    MatchStateChanged?.Invoke(display, true);
                }
            }
        }

        /// <summary>Called when PUBG process exits — clears all state.</summary>
        public void OnGameExited()
        {
            _servers.Clear();
            _primaryIp            = null;
            _primaryPort          = 0;
            _primaryServerDisplay = "--";
            bool wasActive        = _isMatchActive;
            _isMatchActive        = false;

            if (wasActive)
                MatchStateChanged?.Invoke("--", false);
        }

        // ── Cleanup ───────────────────────────────────────────────────────────────

        private void Cleanup(object? _)
        {
            var cutoff = DateTime.UtcNow - ServerTimeout;
            foreach (var key in _servers.Keys)
            {
                if (_servers.TryGetValue(key, out var lastSeen) && lastSeen < cutoff)
                    _servers.TryRemove(key, out DateTime _);
            }

            // If no active servers remain, match has ended
            if (_servers.IsEmpty && _isMatchActive)
            {
                _isMatchActive        = false;
                _primaryServerDisplay = "--";
                _primaryIp            = null;
                MatchStateChanged?.Invoke("--", false);
            }
        }

        // ── Display name builder ──────────────────────────────────────────────────

        private static string BuildDisplayName(IPAddress ip, ushort port)
        {
            string ipStr = ip.ToString();

            // Map known CIDR ranges to friendly names
            if (IsInRange(ip, "20.205.0.0", 16) || IsInRange(ip, "20.43.0.0", 16) ||
                IsInRange(ip, "20.79.0.0",  16) || IsInRange(ip, "20.196.0.0", 16) ||
                IsInRange(ip, "20.201.0.0", 16) || IsInRange(ip, "52.158.0.0", 16))
                return $"Azure SEA — {ipStr}:{port}";

            if (IsInRange(ip, "57.129.0.0", 16))
                return $"PUBG Match Server — {ipStr}:{port}";

            if (IsInRange(ip, "13.228.0.0", 16) || IsInRange(ip, "13.229.0.0", 16) ||
                IsInRange(ip, "18.136.0.0", 16) || IsInRange(ip, "52.74.0.0",  16) ||
                IsInRange(ip, "54.169.0.0", 16))
                return $"AWS Singapore — {ipStr}:{port}";

            return $"{ipStr}:{port}";
        }

        private static bool IsInRange(IPAddress ip, string networkStr, int prefixLen)
        {
            var ipBytes  = ip.GetAddressBytes();
            var netBytes = IPAddress.Parse(networkStr).GetAddressBytes();

            uint ipInt  = (uint)(ipBytes[0]  << 24 | ipBytes[1]  << 16 | ipBytes[2]  << 8 | ipBytes[3]);
            uint netInt = (uint)(netBytes[0] << 24 | netBytes[1] << 16 | netBytes[2] << 8 | netBytes[3]);
            uint mask   = prefixLen == 0 ? 0 : (0xFFFFFFFFu << (32 - prefixLen));

            return (ipInt & mask) == (netInt & mask);
        }

        public void Dispose() => _cleanupTimer.Dispose();
    }
}

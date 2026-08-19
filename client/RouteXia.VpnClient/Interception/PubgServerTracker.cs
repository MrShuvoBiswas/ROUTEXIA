using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using RouteXia.VpnClient.Profiles;

namespace RouteXia.VpnClient.Interception
{
    /// <summary>
    /// Tracks discovered game server IPs and ports from actual intercepted packets for the active game.
    /// Provides real-time "Match Server IP" and location display in ROUTEXIA UI.
    /// </summary>
    public sealed class PubgServerTracker : IDisposable
    {
        // ── Observed servers ──────────────────────────────────────────────────────
        // Key: "ip:port" string, Value: last seen timestamp
        private readonly ConcurrentDictionary<string, DateTime> _servers = new();

        // ── Current primary server ────────────────────────────────────────────────
        private volatile string _primaryServerDisplay = "--";
        private volatile bool   _isMatchActive;
        private IPAddress?      _primaryIp;
        private ushort          _primaryPort;
        private IGameProfile    _activeProfile = new PubgGameProfile();

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

        public void SetActiveProfile(IGameProfile profile)
        {
            _activeProfile = profile ?? new PubgGameProfile();
            _servers.Clear();
            _primaryServerDisplay = "--";
            _isMatchActive = false;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by WinDivertInterceptor every time a game packet is captured.
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
                string display = _activeProfile.FormatServerDisplay(destIp, destPort);

                bool wasActive = _isMatchActive;
                _primaryServerDisplay = display;
                _isMatchActive = true;

                if (!wasActive)
                {
                    MatchStateChanged?.Invoke(display, true);
                }
            }
        }

        /// <summary>Called when game process exits — clears all state.</summary>
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

        public void Dispose() => _cleanupTimer.Dispose();
    }
}

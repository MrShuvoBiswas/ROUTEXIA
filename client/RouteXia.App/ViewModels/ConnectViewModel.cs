using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using RouteXia.VpnClient.Routing;
using RouteXia.VpnClient.KillSwitch;
using RouteXia.VpnClient.Interception;
using RouteXia.VpnClient.Api;

namespace RouteXia.App.ViewModels;

/// <summary>
/// ViewModel for the main Connect view.
/// Drives the UI with real-time multipath routing stats, direct ISP ping comparison,
/// live PUBG match server detection via WinDivert packet capture, and ping reduction indicators.
/// </summary>
public class ConnectViewModel : INotifyPropertyChanged
{
    // ── Services ──────────────────────────────────────────────────────────────
    private readonly MultipathRouter      _router;
    private readonly KillSwitchManager    _killSwitch;
    private readonly SettingsViewModel    _settingsVm;
    private readonly WinDivertInterceptor _interceptor;
    private readonly PubgServerTracker    _serverTracker;
    private readonly RouteXiaApiClient    _apiClient;

    // ── State ─────────────────────────────────────────────────────────────────
    private ConnectionState _state = ConnectionState.Disconnected;
    private DateTimeOffset  _connectedAt;
    private CancellationTokenSource? _connectionCts;
    private Timer? _statsTimer;
    private Timer? _directPingTimer;

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<string>? LogMessage;
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Bindable properties ───────────────────────────────────────────────────

    public ConnectionState State
    {
        get => _state;
        private set { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(StateText)); OnPropertyChanged(nameof(IsConnected)); OnPropertyChanged(nameof(CanConnect)); }
    }

    public bool IsConnected  => State == ConnectionState.Connected;
    public bool CanConnect   => State == ConnectionState.Disconnected;
    public string StateText  => State switch
    {
        ConnectionState.Connected    => "CONNECTED",
        ConnectionState.Connecting   => "CONNECTING...",
        ConnectionState.Disconnected => "DISCONNECTED",
        _ => "UNKNOWN"
    };

    public RouteXiaApiClient ApiClient => _apiClient;

    // PUBG process detection
    private bool _isGameRunning;
    public bool IsGameRunning
    {
        get => _isGameRunning;
        private set { _isGameRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(GameStatusText)); }
    }
    public string GameStatusText => IsGameRunning ? "PUBG PC DETECTED — ACTIVE" : "WAITING FOR PUBG PC...";

    // Live Match Server Info — now driven by real WinDivert packet capture
    private bool _isMatchActive;
    public bool IsMatchActive
    {
        get => _isMatchActive;
        private set { _isMatchActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(MatchStatusText)); }
    }

    private string _matchServerIp = "--";
    public string MatchServerIp
    {
        get => _matchServerIp;
        private set { _matchServerIp = value; OnPropertyChanged(); OnPropertyChanged(nameof(MatchStatusText)); }
    }

    public string MatchStatusText => IsMatchActive
        ? $"🎮 MATCH ACTIVE — {MatchServerIp}"
        : "SEARCHING FOR MATCH...";

    // Direct ISP ping
    private double _directPingMs;
    public double DirectPingMs
    {
        get => _directPingMs;
        private set
        {
            _directPingMs = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DirectPingText));
            OnPropertyChanged(nameof(ImprovementMs));
            OnPropertyChanged(nameof(ImprovementText));
            OnPropertyChanged(nameof(HasImprovement));
        }
    }

    public string DirectPingText => DirectPingMs <= 0 || DirectPingMs >= 999 ? "--" : $"{DirectPingMs:F0}";

    // Kill-switch
    private bool _killSwitchActive;
    public bool KillSwitchActive
    {
        get => _killSwitchActive;
        private set { _killSwitchActive = value; OnPropertyChanged(); }
    }

    // Multipath routing stats
    private int _activeRoutes;
    public int ActiveRoutes
    {
        get => _activeRoutes;
        private set { _activeRoutes = value; OnPropertyChanged(); }
    }

    private double _bestPingMs;
    public double BestPingMs
    {
        get => _bestPingMs;
        private set
        {
            _bestPingMs = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PingText));
            OnPropertyChanged(nameof(ImprovementMs));
            OnPropertyChanged(nameof(ImprovementText));
            OnPropertyChanged(nameof(HasImprovement));
        }
    }

    private double _jitterMs;
    public double JitterMs
    {
        get => _jitterMs;
        private set { _jitterMs = value; OnPropertyChanged(); }
    }

    public string PingText => BestPingMs >= 9999 ? "--" : $"{BestPingMs:F0}";

    public double ImprovementMs => (DirectPingMs > 0 && BestPingMs > 0 && BestPingMs < 9999)
        ? Math.Max(0, DirectPingMs - BestPingMs)
        : 0;

    public string ImprovementText => $"{ImprovementMs:F0}";
    public bool HasImprovement => ImprovementMs > 0;

    private long _sentPackets;
    public long SentPackets
    {
        get => _sentPackets;
        private set { _sentPackets = value; OnPropertyChanged(); }
    }

    private long _interceptedPackets;
    public long InterceptedPackets
    {
        get => _interceptedPackets;
        private set { _interceptedPackets = value; OnPropertyChanged(); }
    }

    private string? _bestRouteName;
    public string? BestRouteName
    {
        get => _bestRouteName;
        private set { _bestRouteName = value; OnPropertyChanged(); }
    }

    public TimeSpan Uptime => IsConnected ? DateTimeOffset.UtcNow - _connectedAt : TimeSpan.Zero;

    // Ping chart data (last 60 samples)
    public ObservableCollection<double> PingHistory { get; } = [];

    // ── Constructor ───────────────────────────────────────────────────────────

    public ConnectViewModel(
        MultipathRouter      router,
        KillSwitchManager    killSwitch,
        SettingsViewModel    settingsVm,
        WinDivertInterceptor interceptor,
        PubgServerTracker    serverTracker,
        RouteXiaApiClient    apiClient)
    {
        _router        = router;
        _killSwitch    = killSwitch;
        _settingsVm    = settingsVm;
        _interceptor   = interceptor;
        _serverTracker = serverTracker;
        _apiClient     = apiClient;

        // Pre-fill chart with 60 zeros
        for (int i = 0; i < 60; i++) PingHistory.Add(0);

        // Fetch dynamic relays from API in background on start
        _ = FetchDynamicRelaysAsync();

        // Wire kill-switch events
        _killSwitch.KillSwitchActivated   += (_, _) => { KillSwitchActive = true;  LogMessage?.Invoke(this, "⚠️  Kill-switch ACTIVATED — PUBG traffic blocked (tunnel down)"); };
        _killSwitch.KillSwitchDeactivated += (_, _) => { KillSwitchActive = false; LogMessage?.Invoke(this, "✅ Kill-switch deactivated — tunnel restored"); };

        // Wire tunnel health to kill-switch
        _killSwitch.SetTunnelHealthCheck(() => IsConnected);

        // ── Wire WinDivert interceptor events ─────────────────────────────────

        // When a PUBG packet is captured → send via multipath relay
        _interceptor.OnPubgPacketCaptured += OnPubgPacketCaptured;

        // When relay returns a response → inject it back to PUBG
        _router.OnRelayResponseReceived += OnRelayResponse;

        // When a new game server IP is discovered → update server tracker
        _interceptor.OnServerDiscovered += _serverTracker.OnPacketObserved;

        // When match state changes → update UI
        _serverTracker.MatchStateChanged += OnMatchStateChanged;

        // Start PUBG process polling + Direct ISP baseline ping polling
        StartGameDetector();
        StartDirectPingPoller();
    }

    private async Task FetchDynamicRelaysAsync()
    {
        try
        {
            var relays = await _apiClient.FetchActiveRelaysAsync();
            if (relays.Count > 0)
            {
                var endpoints = relays.Select(r => new RelayEndpoint(r.Host, (ushort)r.Port, r.RegionCode));
                _router.UpdateRelayEndpoints(endpoints);
                LogMessage?.Invoke(this, $"🌐 Synced {relays.Count} active relay servers from backend");
            }
        }
        catch { /* Fallback to default */ }
    }

    // ── WinDivert event handlers ──────────────────────────────────────────────

    private void OnPubgPacketCaptured(byte[] payload, IPAddress destIp, ushort destPort, ushort srcPort)
    {
        // Only route if connected — otherwise let WinDivert drop the packet
        // (which means the game won't go online, but that's the expected "disconnected" state)
        if (!IsConnected) return;

        _ = _router.SendAsync(payload, destIp, destPort, srcPort);
    }

    private void OnRelayResponse(byte[] payload, IPAddress srcIp, ushort srcPort, ushort localPort)
    {
        // Re-inject relay response back to PUBG, spoofed as originating from the game server
        _interceptor.InjectToGame(payload, srcIp, srcPort, localPort);
    }

    private void OnMatchStateChanged(string serverDisplay, bool isActive)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsMatchActive  = isActive;
            MatchServerIp  = serverDisplay;

            if (isActive)
                LogMessage?.Invoke(this, $"🎯 MATCH ENTERED — Routing through relay to: {serverDisplay}");
            else
                LogMessage?.Invoke(this, "🔚 Match ended.");
        });
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task ConnectAsync()
    {
        if (!CanConnect) return;

        // Check subscription / trial validity
        if (_apiClient.IsAuthenticated && !_apiClient.CanConnect)
        {
            LogMessage?.Invoke(this, "⚠️ Subscription expired. Please renew your plan in the Account tab.");
            return;
        }

        State = ConnectionState.Connecting;
        _connectionCts = new CancellationTokenSource();

        LogMessage?.Invoke(this, "🔗 Measuring relay ping...");

        try
        {
            // Start WinDivert interceptor — this is the real network interception
            _interceptor.Start();
            LogMessage?.Invoke(this, "🔀 WinDivert interception active — capturing PUBG UDP traffic");

            // Wait briefly for first ping measurement
            await Task.Delay(300, _connectionCts.Token);

            // Start relay receive loop
            _router.StartReceiving(_connectionCts.Token);

            State = ConnectionState.Connected;
            _connectedAt = DateTimeOffset.UtcNow;

            StartStatsPoller();
            LogMessage?.Invoke(this, $"✅ Connected — PUBG traffic routed via Singapore relay ({ActiveRoutes} parallel paths)");
        }
        catch (OperationCanceledException)
        {
            State = ConnectionState.Disconnected;
        }
        catch (Exception ex)
        {
            // If WinDivert fails (DLL missing, not admin, etc.) — show clear error
            State = ConnectionState.Disconnected;
            LogMessage?.Invoke(this, $"❌ Connection failed: {ex.Message}");
            Debug.WriteLine($"[ConnectVM] Connect error: {ex}");
        }
    }

    public Task DisconnectAsync()
    {
        _connectionCts?.Cancel();
        _statsTimer?.Dispose();
        _statsTimer = null;

        // Stop WinDivert interception
        _interceptor.Stop();

        // Stop relay receive loop
        _router.StopReceiving();

        // Reset match state
        _serverTracker.OnGameExited();

        State = ConnectionState.Disconnected;

        // Reset stats
        BestPingMs          = 0;
        JitterMs            = 0;
        ActiveRoutes        = 0;
        SentPackets         = 0;
        InterceptedPackets  = 0;
        for (int i = 0; i < PingHistory.Count; i++) PingHistory[i] = 0;

        LogMessage?.Invoke(this, "🔌 Disconnected — PUBG traffic returned to normal routing");
        return Task.CompletedTask;
    }

    // ── Stats polling ─────────────────────────────────────────────────────────

    private void StartStatsPoller()
    {
        _statsTimer = new Timer(UpdateStats, null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void UpdateStats(object? _)
    {
        var stats = _router.Stats;

        BestPingMs         = stats.BestRoutePing;
        JitterMs           = stats.BestRouteJitter;
        ActiveRoutes       = stats.ActiveRoutes;
        SentPackets        = stats.SentPackets;
        InterceptedPackets = _interceptor.PacketsCaptured;
        BestRouteName      = stats.LastSentRoute;

        // Update ping chart
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (PingHistory.Count >= 60) PingHistory.RemoveAt(0);
            PingHistory.Add(BestPingMs < 9999 ? BestPingMs : 0);
        });

        OnPropertyChanged(nameof(Uptime));
    }

    // ── Direct ISP baseline ping poller ───────────────────────────────────────

    private void StartDirectPingPoller()
    {
        _directPingTimer = new Timer(async _ =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var client = new System.Net.Sockets.TcpClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                await client.ConnectAsync("1.1.1.1", 53, cts.Token);
                sw.Stop();
                DirectPingMs = sw.Elapsed.TotalMilliseconds;
            }
            catch
            {
                try
                {
                    using var pinger = new Ping();
                    var reply = await pinger.SendPingAsync("8.8.8.8", 1500);
                    if (reply.Status == IPStatus.Success)
                    {
                        DirectPingMs = reply.RoundtripTime;
                    }
                }
                catch { /* Best effort */ }
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    // ── PUBG PC process detection ──────────────────────────────────────────────

    private static readonly string[] PubgProcessNames = ["TslGame"];

    private Timer? _gameDetectTimer;

    private void StartGameDetector()
    {
        _gameDetectTimer = new Timer(DetectGame, null,
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void DetectGame(object? _)
    {
        bool found = false;
        foreach (var name in PubgProcessNames)
        {
            var procs = Process.GetProcessesByName(name);
            if (procs.Length > 0)
            {
                found = true;
                // Dispose all process handles
                foreach (var p in procs) p.Dispose();
                break;
            }
        }

        if (found != IsGameRunning)
        {
            IsGameRunning = found;
            LogMessage?.Invoke(this, found
                ? "🎮 PUBG PC detected — WinDivert ready to intercept traffic!"
                : "🎮 PUBG PC closed.");

            if (!found)
            {
                // Game closed — clear match state
                _serverTracker.OnGameExited();
            }

            if (found && _settingsVm.AutoConnectOnGameLaunch && CanConnect)
            {
                LogMessage?.Invoke(this, "⚡ Auto-connecting...");
                _ = ConnectAsync();
            }
        }
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Connection state for the RouteXia client.</summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected
}

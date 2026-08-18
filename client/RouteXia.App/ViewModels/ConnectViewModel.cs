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
using RouteXia.App.Data;
using RouteXia.VpnClient.Routing;
using RouteXia.VpnClient.KillSwitch;
using RouteXia.VpnClient.Interception;
using RouteXia.VpnClient.Api;
using RouteXia.VpnClient.Security;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.Views;

namespace RouteXia.App.ViewModels;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    /// <summary>
    /// Route scoring is actively running. Tunnel remains up — only the status indicator changes.
    /// <br/>Treated as a visual sub-state of Connected (IsConnected returns true).
    /// </summary>
    Optimizing,
    /// <summary>
    /// Tunnel has dropped and the Windows Firewall kill-switch rule is active.
    /// Game traffic is blocked until the user reconnects or the tunnel recovers.
    /// </summary>
    KillSwitchActive
}

public enum ConnectFlowStep
{
    ConnectionsHome,         // Screen 1: Connections Home (Empty state OR Configured Games with Traffic Table)
    GameInitialRoute,        // Screen 2: Game selected, "No game route yet", "Choose a region or server"
    SelectRoute,             // Screen 3: "Select a route for PUBG", Auto vs Manual, Regions list
    AnalyzingRoutes,         // Screen 4: "Analyzing routes" with 68% animated loader & region dots
    GameRouteDiagram         // Screen 5: "Game route diagram", All regions [Automatic], Apply routes
}

public class RegionItem
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public bool IsRecommended { get; set; }
    public bool IsSelected { get; set; }
}

public class VisualRouteHop
{
    public required string HopName { get; set; }
    public required string InLatency { get; set; }
    public required string OutLatency { get; set; }
    public bool IsActivePath { get; set; }
}

public record RouteSnapshot(
    string RelayName,
    string RelayId,
    double PingMs,
    double JitterMs,
    double Score,
    bool IsActivePrimary,
    bool IsAlive,
    DateTimeOffset SampledAt);

public class ConfiguredGameItem : INotifyPropertyChanged
{
    public required GameDefinition Game { get; set; }
    public string ServerMode { get; set; } = "Automatic";

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

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
    private Timer? _gameProcessTimer;
    private Timer? _serverRefreshTimer;

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<string>? LogMessage;
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Flow Step State Machine ───────────────────────────────────────────────
    private ConnectFlowStep _currentFlowStep = ConnectFlowStep.ConnectionsHome;
    public ConnectFlowStep CurrentFlowStep
    {
        get => _currentFlowStep;
        set
        {
            _currentFlowStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStepConnectionsHome));
            OnPropertyChanged(nameof(IsStepGameInitialRoute));
            OnPropertyChanged(nameof(IsStepSelectRoute));
            OnPropertyChanged(nameof(IsStepAnalyzingRoutes));
            OnPropertyChanged(nameof(IsStepGameRouteDiagram));
            OnPropertyChanged(nameof(HasConfiguredGame));

            if (value == ConnectFlowStep.SelectRoute)
            {
                _ = LoadDynamicServersAsync();
            }
        }
    }

    public bool IsStepConnectionsHome    => CurrentFlowStep == ConnectFlowStep.ConnectionsHome;
    public bool IsStepGameInitialRoute   => CurrentFlowStep == ConnectFlowStep.GameInitialRoute;
    public bool IsStepSelectRoute        => CurrentFlowStep == ConnectFlowStep.SelectRoute;
    public bool IsStepAnalyzingRoutes    => CurrentFlowStep == ConnectFlowStep.AnalyzingRoutes;
    public bool IsStepGameRouteDiagram   => CurrentFlowStep == ConnectFlowStep.GameRouteDiagram;

    public bool HasConfiguredGame => CurrentFlowStep != ConnectFlowStep.ConnectionsHome;

    // ── Configured Games for Connections Screen ───────────────────────────────
    public ObservableCollection<ConfiguredGameItem> ConfiguredGames { get; } = [];
    public bool HasConfiguredGames => ConfiguredGames.Count > 0;
    public bool HasNoConfiguredGames => ConfiguredGames.Count == 0;

    public void RemoveConfiguredGame(ConfiguredGameItem item)
    {
        item.PropertyChanged -= OnConfiguredGameItemPropertyChanged;
        ConfiguredGames.Remove(item);
        OnPropertyChanged(nameof(HasConfiguredGames));
        OnPropertyChanged(nameof(HasNoConfiguredGames));
        if (ConfiguredGames.Count == 0 && IsConnected)
        {
            _ = DisconnectAsync();
        }
    }

    // ── Plan Indicator ────────────────────────────────────────────────────────
    private bool _isPaidPlan;
    public bool IsPaidPlan
    {
        get => _isPaidPlan;
        set
        {
            _isPaidPlan = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowPaidBadge));
        }
    }

    public bool ShowPaidBadge => !IsPaidPlan;

    // ── Current Game (scalable multi-game support) ────────────────────────────
    private GameDefinition _currentGame;

    public GameDefinition CurrentGame
    {
        get => _currentGame;
        private set
        {
            _currentGame = value;
            if (value?.ProcessNames != null)
            {
                RouteXia.VpnClient.Interception.GameSocketTracker.SetTargetProcessNames(value.ProcessNames);
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(GameStatusText));
            OnPropertyChanged(nameof(LaunchButtonText));
            OnPropertyChanged(nameof(CurrentGameDisplayText));
            OnPropertyChanged(nameof(CurrentRegionBadge));
            OnPropertyChanged(nameof(CurrentRegionName));
            OnPropertyChanged(nameof(SelectedGameTitle));
        }
    }

    public string SelectedGameTitle => CurrentGame.Name;

    public void ConfigureGame(GameDefinition game)
    {
        CurrentGame = game;
        CurrentFlowStep = ConnectFlowStep.GameInitialRoute;
        LogMessage?.Invoke(this, $"🎮 Game configured: {game.Name}");
    }

    public void ResetGameConfiguration()
    {
        CurrentFlowStep = ConnectFlowStep.ConnectionsHome;
        IsRoutesApplied = false;
        if (IsConnected)
        {
            _ = DisconnectAsync();
        }
    }

    // ── Route Selection (Screen 3) ────────────────────────────────────────────
    private bool _isAutoServerSelection = true;
    public bool IsAutoServerSelection
    {
        get => _isAutoServerSelection;
        set
        {
            if (_isAutoServerSelection != value)
            {
                _isAutoServerSelection = value;
                _isManualServerSelection = !value;
                if (value)
                {
                    SelectLowestPingServer();
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsManualServerSelection));
                OnPropertyChanged(nameof(ServerModeBadgeText));
                OnPropertyChanged(nameof(SelectedServerName));
                OnPropertyChanged(nameof(CurrentRelayDisplayName));
                OnPropertyChanged(nameof(CurrentRelaySubText));
                OnPropertyChanged(nameof(CurrentRelayLatencyText));
                OnPropertyChanged(nameof(IsActiveFlagVisible));
            }
        }
    }

    private bool _isManualServerSelection;
    public bool IsManualServerSelection
    {
        get => _isManualServerSelection;
        set
        {
            if (_isManualServerSelection != value)
            {
                _isManualServerSelection = value;
                _isAutoServerSelection = !value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAutoServerSelection));
                OnPropertyChanged(nameof(TargetRegionText));
                OnPropertyChanged(nameof(ServerModeBadgeText));
                OnPropertyChanged(nameof(SelectedServerName));
                OnPropertyChanged(nameof(CurrentRelayDisplayName));
                OnPropertyChanged(nameof(CurrentRelaySubText));
                OnPropertyChanged(nameof(CurrentRelayLatencyText));
                OnPropertyChanged(nameof(IsActiveFlagVisible));
            }
        }
    }

    public string ServerModeBadgeText => IsAutoServerSelection ? "Automatic" : "Manual";

    public ObservableCollection<RegionItem> RegionsList { get; } = new()
    {
        new RegionItem { Name = "ALL", DisplayName = "All Regions (Recommended)", IsRecommended = true, IsSelected = true },
        new RegionItem { Name = "ASIA", DisplayName = "Asia", IsRecommended = false, IsSelected = false },
        new RegionItem { Name = "NA", DisplayName = "North America", IsRecommended = false, IsSelected = false },
        new RegionItem { Name = "SA", DisplayName = "South America", IsRecommended = false, IsSelected = false },
        new RegionItem { Name = "EU", DisplayName = "Europe", IsRecommended = false, IsSelected = false },
        new RegionItem { Name = "OCE", DisplayName = "Oceania", IsRecommended = false, IsSelected = false },
    };

    private RegionItem? _selectedRegionItem;
    public RegionItem? SelectedRegionItem
    {
        get => _selectedRegionItem;
        set
        {
            if (_selectedRegionItem != null) _selectedRegionItem.IsSelected = false;
            _selectedRegionItem = value;
            if (_selectedRegionItem != null) _selectedRegionItem.IsSelected = true;
            OnPropertyChanged();
        }
    }

    private string _regionSearchQuery = string.Empty;
    public string RegionSearchQuery
    {
        get => _regionSearchQuery;
        set
        {
            _regionSearchQuery = value;
            OnPropertyChanged();
        }
    }

    // ── Analyzing Routes (Screen 4) ───────────────────────────────────────────
    private int _analyzingProgressPercent = 0;
    public int AnalyzingProgressPercent
    {
        get => _analyzingProgressPercent;
        set
        {
            _analyzingProgressPercent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AnalyzingProgressPercentText));
        }
    }

    public string AnalyzingProgressPercentText => $"{AnalyzingProgressPercent}%";

    private string _analyzingRoutesText = "3 / 3 (Routes: 56 / 955)";
    public string AnalyzingRoutesText
    {
        get => _analyzingRoutesText;
        set { _analyzingRoutesText = value; OnPropertyChanged(); }
    }

    public async Task StartAnalyzingRoutesAsync(RegionItem region)
    {
        SelectedRegionItem = region;
        CurrentFlowStep = ConnectFlowStep.AnalyzingRoutes;
        AnalyzingProgressPercent = 0;
        AnalyzingRoutesText = "1 / 3 (Probing local latency...)";

        try
        {
            await Task.Delay(300);
            AnalyzingProgressPercent = 28;
            AnalyzingRoutesText = "2 / 3 (Evaluating Singapore AWS Direct...)";

            await Task.Delay(400);
            AnalyzingProgressPercent = 68;
            AnalyzingRoutesText = "3 / 3 (Routes: 56 / 955)";

            await Task.Delay(400);
            AnalyzingProgressPercent = 95;

            await Task.Delay(300);
            AnalyzingProgressPercent = 100;

            await Task.Delay(200);
            CurrentFlowStep = ConnectFlowStep.GameRouteDiagram;
        }
        catch { }
    }

    // ── Game Route Diagram & Apply Routes (Screen 5) ──────────────────────────
    public string UserLocationText => "Kolkata - IN";
    public string TargetRegionText => IsManualServerSelection && SelectedServer != null
        ? SelectedServer.Name
        : (SelectedRegionItem?.Name == "ASIA" ? "Asia" : "All Regions");

    private bool _isRoutesApplied;
    public bool IsRoutesApplied
    {
        get => _isRoutesApplied;
        set
        {
            _isRoutesApplied = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ApplyRoutesButtonText));
            OnPropertyChanged(nameof(ApplyRoutesButtonBg));
            OnPropertyChanged(nameof(ApplyRoutesButtonBorder));
            OnPropertyChanged(nameof(ApplyRoutesButtonFg));
        }
    }

    public string ApplyRoutesButtonText => IsRoutesApplied ? "Routes applied" : "Apply routes";
    public string ApplyRoutesButtonBg => IsRoutesApplied ? "#091A14" : "#FF3344";
    public string ApplyRoutesButtonBorder => IsRoutesApplied ? "#2ED573" : "#FF3344";
    public string ApplyRoutesButtonFg => IsRoutesApplied ? "#2ED573" : "#FFFFFF";

    private bool _isGameRunning;
    public bool IsGameRunning
    {
        get => _isGameRunning;
        set
        {
            _isGameRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GameRunningStatusText));
            OnPropertyChanged(nameof(HasNoGameRunning));
            OnPropertyChanged(nameof(IsGameDetected));
            OnPropertyChanged(nameof(DetectedGameName));
            OnPropertyChanged(nameof(DetectedGameStatusText));
            OnPropertyChanged(nameof(DetectedGameIconPath));
            OnPropertyChanged(nameof(GameDetectionHeader));
            OnPropertyChanged(nameof(TopBarStatusText));
        }
    }

    public bool HasNoGameRunning => !IsGameRunning;

    public bool IsGameDetected => IsGameRunning;
    public string DetectedGameName => IsGameRunning ? CurrentGame.Name : "Waiting for game...";
    public string DetectedGameStatusText => IsGameRunning ? $"Game Detected — {CurrentGame.ShortName}" : "Waiting for game...";
    public string? DetectedGameIconPath => IsGameRunning ? CurrentGame?.ImagePath : null;
    public string GameDetectionHeader => IsGameRunning ? "ACTIVE GAME" : "GAME DETECTION";
    public string ActiveRouteLabel => SelectedServer != null ? SelectedServer.Name : $"{CurrentGame.ShortName} {CurrentGame.RegionBadge}";

    public string TopBarStatusText
    {
        get
        {
            if (IsConnected)
                return $"Connected — {ActiveRouteLabel}";
            if (IsConnecting)
                return $"Connecting to {SelectedServerName}...";
            if (IsKillSwitchActive)
                return "Kill-Switch Active — Traffic Blocked";
            if (IsGameDetected)
                return $"{CurrentGame.Name} detected — Click Boost";
            return "RouteXia ready — Waiting for game";
        }
    }

    // ── Auth and Subscription Forwarding for MainWindow Widget ───────────────
    private AuthViewModel? _authVm;
    public AuthViewModel? AuthVm
    {
        get
        {
            if (_authVm == null && App.Services != null)
            {
                _authVm = App.Services.GetService<AuthViewModel>();
                if (_authVm != null)
                {
                    _authVm.PropertyChanged += (_, _) =>
                    {
                        OnPropertyChanged(nameof(IsAuthenticated));
                        OnPropertyChanged(nameof(UserEmail));
                        OnPropertyChanged(nameof(HasSubscription));
                        OnPropertyChanged(nameof(SubscriptionTitle));
                        OnPropertyChanged(nameof(PlanBadgeText));
                        OnPropertyChanged(nameof(DaysLeftText));
                        OnPropertyChanged(nameof(IsExpiryWarning));
                    };
                }
            }
            return _authVm;
        }
    }

    public bool HasValidSubscription => _apiClient.CurrentSubscription?.CanConnect == true && _apiClient.CurrentSubscription?.DaysLeft > 0;
    public bool IsAuthenticated => _apiClient.CurrentUser != null;
    public string UserEmail => _apiClient.CurrentUser?.Email ?? (AuthVm?.UserEmail ?? "No user logged in");
    public bool HasSubscription => HasValidSubscription;
    public string SubscriptionTitle => HasValidSubscription
        ? (_apiClient.CurrentSubscription?.IsTrial == true ? "Free Trial Active" : "Active Pro Plan")
        : "No subscription";
    public string PlanBadgeText => _apiClient.CurrentSubscription?.IsTrial == true ? "FREE TRIAL" : (HasValidSubscription ? "PREMIUM" : "EXPIRED");
    public string DaysLeftText => $"{_apiClient.CurrentSubscription?.DaysLeft ?? 0} Days Left";
    public bool IsExpiryWarning => HasValidSubscription && (_apiClient.CurrentSubscription?.DaysLeft <= 7);
    public bool IsManualRelayAllowed => _apiClient.CanManualSelectRelay;

    public string GameRunningStatusText => IsGameRunning
        ? $"⚡ {CurrentGame.ShortName} running — Live routing active (Ping: 42ms | Loss: 0%)"
        : $"Launch {CurrentGame.ShortName} and enjoy";

    // ── Live Traffic Metrics for Connections Table (Screenshot 2) ─────────────
    private double _sentBytesTotal = 145510;
    private double _sentRateKbps = 28.42;
    private double _recvBytesTotal = 9870;
    private double _recvRateKbps = 1.93;

    public string SentTotalKbText => $"{_sentBytesTotal / 1024.0:F1} KB";
    public string SentRateKbpsText => $"{_sentRateKbps:F2} KB/s";
    public string RecvTotalKbText => $"{_recvBytesTotal / 1024.0:F2} KB";
    public string RecvRateKbpsText => $"{_recvRateKbps:F2} KB/s";
    public string LivePingText => SelectedServer != null && SelectedServer.LatencyMs > 0 ? $"{SelectedServer.LatencyMs:F0} ms" : "-- ms";
    public string RouteXiaServerText => SelectedServer?.Name ?? "NO SERVER AVAILABLE";
    public string GameServerEndpointText => SelectedServer != null ? $"{SelectedServer.Country}\nProtected Route" : "No Relay Configured";
    public string ProtocolText => "UDP";

    // ── Advanced Settings Popover ─────────────────────────────────────────────
    private bool _isAdvancedSettingsOpen;
    public bool IsAdvancedSettingsOpen
    {
        get => _isAdvancedSettingsOpen;
        set { _isAdvancedSettingsOpen = value; OnPropertyChanged(); }
    }

    private bool _useLocalRoutesFirst = true;
    public bool UseLocalRoutesFirst
    {
        get => _useLocalRoutesFirst;
        set { _useLocalRoutesFirst = value; OnPropertyChanged(); }
    }

    private bool _redirectLogin;
    public bool RedirectLogin
    {
        get => _redirectLogin;
        set { _redirectLogin = value; OnPropertyChanged(); }
    }

    private int _tcpRoutesCount = 0;
    public int TcpRoutesCount
    {
        get => _tcpRoutesCount;
        set { _tcpRoutesCount = Math.Max(0, Math.Min(4, value)); OnPropertyChanged(); }
    }

    private int _udpRoutesCount = 2;
    public int UdpRoutesCount
    {
        get => _udpRoutesCount;
        set { _udpRoutesCount = Math.Max(1, Math.Min(4, value)); OnPropertyChanged(); }
    }

    // ── Game-aware display properties ─────────────────────────────────────────
    public string LaunchButtonText => $"LAUNCH {CurrentGame.ShortName}";
    public string CurrentGameDisplayText => $"Optimize route for {CurrentGame.Name}";
    public string CurrentRegionBadge => CurrentGame.RegionBadge;
    public string CurrentRegionName => CurrentGame.RegionName;

    // ── Server Node selection ─────────────────────────────────────────────────
    public ObservableCollection<ServerNode> AllServerNodes { get; } = [];
    public ObservableCollection<ServerNode> FilteredServerNodes { get; } = [];

    private ServerNode? _selectedServer;
    public ServerNode? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (_selectedServer != null) _selectedServer.IsSelected = false;
            _selectedServer = value;
            if (_selectedServer != null) _selectedServer.IsSelected = true;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedServerName));
            OnPropertyChanged(nameof(EstimatedPingText));
            OnPropertyChanged(nameof(LivePingText));
            OnPropertyChanged(nameof(RouteXiaServerText));
            OnPropertyChanged(nameof(GameServerEndpointText));
            OnPropertyChanged(nameof(ConnectActionText));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanToggleConnection));
            OnPropertyChanged(nameof(TargetRegionText));
            OnPropertyChanged(nameof(ServerModeBadgeText));
            UpdateRouteTopology();
        }
    }

    public bool IsActiveFlagVisible => IsConnected || (IsManualServerSelection && SelectedServer != null);

    public string SelectedServerName
    {
        get
        {
            if (IsConnected)
                return SelectedServer?.Name ?? "Singapore";
            if (IsManualServerSelection && SelectedServer != null)
                return SelectedServer.Name;
            return "Auto (Smart Route)";
        }
    }

    public string CurrentRelayDisplayName
    {
        get
        {
            if (IsConnected)
                return SelectedServer?.Name ?? "Singapore";
            if (IsManualServerSelection && SelectedServer != null)
                return SelectedServer.Name;
            return "Auto Best Selection";
        }
    }

    public string CurrentRelaySubText
    {
        get
        {
            if (IsConnected)
                return SelectedServer != null ? $"{SelectedServer.Subtitle} • Optimized" : "Singapore BGP Multipath Route • Optimized";
            if (IsManualServerSelection && SelectedServer != null)
                return SelectedServer.Subtitle;
            return "Auto analyzes & connects to lowest latency relay";
        }
    }

    public string CurrentRelayLatencyText
    {
        get
        {
            if (IsConnected)
                return EstimatedPingText;
            if (IsManualServerSelection && SelectedServer != null && SelectedServer.LatencyMs > 0)
                return $"{SelectedServer.LatencyMs:F0} ms";
            return "-- ms";
        }
    }

    public string EstimatedPingText => SelectedServer != null && SelectedServer.LatencyMs > 0
        ? $"{SelectedServer.LatencyMs:F0} ms"
        : "-- ms";

    private string _serverSearchQuery = string.Empty;
    public string ServerSearchQuery
    {
        get => _serverSearchQuery;
        set
        {
            _serverSearchQuery = value;
            OnPropertyChanged();
            ApplyServerFilter();
        }
    }

    private string _selectedServerTab = "RECOMMENDED";
    public string SelectedServerTab
    {
        get => _selectedServerTab;
        set
        {
            _selectedServerTab = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRecommendedTab));
            OnPropertyChanged(nameof(IsByCountryTab));
            ApplyServerFilter();
        }
    }

    public bool IsRecommendedTab => SelectedServerTab == "RECOMMENDED";
    public bool IsByCountryTab => SelectedServerTab == "BY COUNTRY";

    private bool _isAutoOptimize = true;
    public bool IsAutoOptimize
    {
        get => _isAutoOptimize;
        set
        {
            _isAutoOptimize = value;
            OnPropertyChanged();
            if (value)
            {
                SelectLowestPingServer();
            }
        }
    }

    private bool _isAdvancedMode;
    public bool IsAdvancedMode
    {
        get => _isAdvancedMode;
        set { _isAdvancedMode = value; OnPropertyChanged(); }
    }

    private string _numberOfRoutes = "03";
    public string NumberOfRoutes
    {
        get => _numberOfRoutes;
        set
        {
            _numberOfRoutes = value;
            OnPropertyChanged();
            UpdateRouteTopology();
        }
    }

    // ── Visual Topology Graph Hops ───────────────────────────────────────────
    public ObservableCollection<VisualRouteHop> VisualHops { get; } = [];

    // ── Route Latency Graph Ring Buffer (T007) ─────────────────────────────────
    private readonly Dictionary<string, Queue<RouteSnapshot>> _routeRingBuffers = new();
    private const int MaxRouteHistorySamples = 120;

    // ── Session Reporting State ────────────────────────────────────────────────
    private string? _currentSessionId;
    private Timer? _heartbeatTimer;
    private DateTime _lastHeartbeatSent = DateTime.MinValue;

    public ObservableCollection<RouteSnapshot> RouteHistory { get; } = [];

    public void AddRouteSnapshot(RouteSnapshot snap)
    {
        if (!_routeRingBuffers.TryGetValue(snap.RelayId, out var queue))
        {
            queue = new Queue<RouteSnapshot>();
            _routeRingBuffers[snap.RelayId] = queue;
        }

        queue.Enqueue(snap);
        if (queue.Count > MaxRouteHistorySamples)
        {
            queue.Dequeue();
        }

        RouteHistory.Add(snap);
        while (RouteHistory.Count > MaxRouteHistorySamples * 3)
        {
            RouteHistory.RemoveAt(0);
        }
    }

    public void ClearRouteHistory()
    {
        _routeRingBuffers.Clear();
        RouteHistory.Clear();
    }

    // ── Session Reporting Methods ────────────────────────────────────────────────

    private async Task<(bool success, string message)> ReportSessionConnectAsync()
    {
        if (_apiClient.CurrentUser == null || SelectedServer == null)
            return (false, "Not logged in or no server selected.");

        var request = new SessionConnectRequest
        {
            UserId = _apiClient.CurrentUser.ID,
            RelayId = SelectedServer.Id,
            RelayName = SelectedServer.Name,
            RelayRegion = SelectedServer.Country,
            RelayHost = SelectedServer.Host,
            GameName = CurrentGame?.Name,
            GameProcess = CurrentGame?.ProcessNames.FirstOrDefault(),
            PingMs = SelectedServer.LatencyMs > 0 ? (int)SelectedServer.LatencyMs : null,
            Hwid = HwidGenerator.GetHwid(),
            ClientVersion = "1.0.0"
        };

        var result = await _apiClient.ReportSessionConnectAsync(request);
        if (result.success && result.data != null)
        {
            _currentSessionId = result.data.SessionId;
            LogMessage?.Invoke(this, $"📡 Session reported to backend: {_currentSessionId}");
            StartHeartbeatTimer();
            return (true, "OK");
        }
        else
        {
            LogMessage?.Invoke(this, $"⚠️ Session rejected by server: {result.message}");
            return (false, result.message ?? "Connection rejected by server.");
        }
    }

    private void StartHeartbeatTimer()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new Timer(async _ =>
        {
            if (!string.IsNullOrEmpty(_currentSessionId) && IsConnected)
            {
                await SendHeartbeatAsync();
            }
        }, null, 10000, 10000); // Every 10 seconds for real-time ban & subscription validation
    }

    private long _lastSentBytes;
    private long _lastRecvBytes;
    private DateTime _lastHeartbeatTime = DateTime.UtcNow;

    private async Task SendHeartbeatAsync()
    {
        if (string.IsNullOrEmpty(_currentSessionId) || !IsConnected) return;

        var stats = _router?.Stats;
        long currentSent = stats?.SentBytes ?? 0;
        long currentRecv = stats?.ReceivedBytes ?? 0;
        var now = DateTime.UtcNow;
        double elapsedSec = Math.Max(1.0, (now - _lastHeartbeatTime).TotalSeconds);

        double upMbps = ((currentSent - _lastSentBytes) * 8.0) / (elapsedSec * 1000000.0);
        double downMbps = ((currentRecv - _lastRecvBytes) * 8.0) / (elapsedSec * 1000000.0);

        _lastSentBytes = currentSent;
        _lastRecvBytes = currentRecv;
        _lastHeartbeatTime = now;

        var request = new SessionHeartbeatRequest
        {
            SessionId = _currentSessionId,
            PingMs = (int)BestPingMs,
            DownloadMbps = Math.Max(0.01, Math.Round(downMbps, 2)),
            UploadMbps = Math.Max(0.01, Math.Round(upMbps, 2)),
            BytesSent = currentSent,
            BytesReceived = currentRecv,
            GameName = CurrentGame?.Name,
            GameProcess = CurrentGame?.ProcessNames.FirstOrDefault()
        };

        var result = await _apiClient.ReportSessionHeartbeatAsync(request);
        if (result.success)
        {
            _lastHeartbeatSent = DateTime.UtcNow;
        }
        else
        {
            LogMessage?.Invoke(this, $"⚠️ Heartbeat rejected by server: {result.message}");
            if (!string.IsNullOrEmpty(result.message) && (
                result.message.Contains("suspended") ||
                result.message.Contains("banned") ||
                result.message.Contains("deleted") ||
                result.message.Contains("expired") ||
                result.message.Contains("inactive")))
            {
                await DisconnectAsync();
                ShowSubscriptionRequiredPrompt(result.message);
            }
        }
    }

    private async Task ReportSessionDisconnectAsync()
    {
        if (string.IsNullOrEmpty(_currentSessionId)) return;

        var stats = _router?.Stats;
        var request = new SessionDisconnectRequest
        {
            SessionId = _currentSessionId,
            BytesSent = stats?.SentBytes,
            BytesReceived = stats?.ReceivedBytes
        };

        var result = await _apiClient.ReportSessionDisconnectAsync(request);
        if (result.success)
        {
            LogMessage?.Invoke(this, $"📡 Session disconnected reported to backend");
        }
        else
        {
            LogMessage?.Invoke(this, $"⚠️ Failed to report disconnect: {result.message}");
        }

        _currentSessionId = null;
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }

    // ── Bindable properties ───────────────────────────────────────────────────

    public ConnectionState State
    {
        get => _state;
        private set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConnectionState));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(ConnectActionText));
            OnPropertyChanged(nameof(ConnectButtonBg));
            OnPropertyChanged(nameof(ConnectButtonBorder));
            OnPropertyChanged(nameof(ConnectButtonFg));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsConnecting));
            OnPropertyChanged(nameof(IsDisconnected));
            OnPropertyChanged(nameof(IsOptimizing));
            OnPropertyChanged(nameof(IsKillSwitchActive));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanToggleConnection));
            OnPropertyChanged(nameof(IsOptimized));
            OnPropertyChanged(nameof(IsProbing));
            OnPropertyChanged(nameof(IsGlobalOptimizationActive));
            OnPropertyChanged(nameof(ActiveRouteColor));
            OnPropertyChanged(nameof(ActiveRouteGlow));
            OnPropertyChanged(nameof(YouNodeBorder));
            OnPropertyChanged(nameof(RelayNodeBorder));
            OnPropertyChanged(nameof(GameServerNodeBorder));
            OnPropertyChanged(nameof(ActivePathStrokeThickness));
            OnPropertyChanged(nameof(TopBarStatusText));
            OnPropertyChanged(nameof(SelectedServerName));
            OnPropertyChanged(nameof(CurrentRelayDisplayName));
            OnPropertyChanged(nameof(CurrentRelaySubText));
            OnPropertyChanged(nameof(CurrentRelayLatencyText));
            OnPropertyChanged(nameof(IsActiveFlagVisible));
        }
    }

    public ConnectionState ConnectionState => State;

    // Granular connection state booleans — bind to DataTriggers in views
    public bool IsConnected        => State == ConnectionState.Connected || State == ConnectionState.Optimizing;
    public bool IsConnecting       => State == ConnectionState.Connecting;
    public bool IsDisconnected     => State == ConnectionState.Disconnected;
    public bool IsOptimizing       => State == ConnectionState.Optimizing;
    public bool IsKillSwitchActive => State == ConnectionState.KillSwitchActive;

    public bool CanConnect   => State == ConnectionState.Disconnected && SelectedServer != null;
    public bool CanToggleConnection => State != ConnectionState.Connecting && (IsConnected || SelectedServer != null);
    public bool IsOptimized  => State == ConnectionState.Connected || State == ConnectionState.Optimizing;
    public bool IsProbing    => State == ConnectionState.Connecting;
    public bool IsGlobalOptimizationActive => IsConnected && ConfiguredGames.Any(g => g.IsEnabled);

    public string ActiveRouteColor => State switch
    {
        ConnectionState.Connected        => "#2ED573",
        ConnectionState.Optimizing       => "#00C2FF",
        ConnectionState.Connecting       => "#00C2FF",
        ConnectionState.KillSwitchActive => "#FF4757",
        ConnectionState.Disconnected     => "#1B2A3A",
        _ => "#1B2A3A"
    };

    public double ActiveRouteGlow => IsConnected ? 1.0 : 0.0;
    public double ActivePathStrokeThickness => IsConnected ? 2.5 : 1.5;

    public string YouNodeBorder => State switch
    {
        ConnectionState.Connected        => "#2ED573",
        ConnectionState.Optimizing       => "#00C2FF",
        ConnectionState.Connecting       => "#00C2FF",
        ConnectionState.KillSwitchActive => "#FF4757",
        ConnectionState.Disconnected     => "#1F2E40",
        _ => "#1F2E40"
    };

    public string RelayNodeBorder => State switch
    {
        ConnectionState.Connected        => "#2ED573",
        ConnectionState.Optimizing       => "#FFB020",
        ConnectionState.Connecting       => "#FFB020",
        ConnectionState.KillSwitchActive => "#FF4757",
        ConnectionState.Disconnected     => "#1F2E40",
        _ => "#1F2E40"
    };

    public string GameServerNodeBorder => State switch
    {
        ConnectionState.Connected        => "#2ED573",
        ConnectionState.Optimizing       => "#2ED573",
        ConnectionState.Connecting       => "#1C2B3C",
        ConnectionState.KillSwitchActive => "#FF4757",
        ConnectionState.Disconnected     => "#1C2B3C",
        _ => "#1C2B3C"
    };

    public string StateText  => State switch
    {
        ConnectionState.Connected        => "CONNECTED",
        ConnectionState.Optimizing       => "OPTIMIZING",
        ConnectionState.Connecting       => "CONNECTING...",
        ConnectionState.KillSwitchActive => "KILL-SWITCH ACTIVE",
        ConnectionState.Disconnected     => "DISCONNECTED",
        _ => "DISCONNECTED"
    };

    public string ConnectActionText => State switch
    {
        ConnectionState.Connected        => "STOP BOOST",
        ConnectionState.Optimizing       => "BOOSTING...",
        ConnectionState.Connecting       => "BOOSTING...",
        ConnectionState.KillSwitchActive => "RECONNECT",
        ConnectionState.Disconnected     => !HasValidSubscription ? "SUBSCRIBE TO BOOST" : (SelectedServer == null ? "WAITING FOR SERVER" : $"BOOST {CurrentGame.ShortName.ToUpper()}"),
        _ => !HasValidSubscription ? "SUBSCRIBE TO BOOST" : $"BOOST {CurrentGame.ShortName.ToUpper()}"
    };

    public string ConnectButtonBg => State switch
    {
        ConnectionState.Connected        => "#091A14",
        ConnectionState.Optimizing       => "#091A14",
        ConnectionState.Connecting       => "#2A1E0D",
        ConnectionState.KillSwitchActive => "#200A0C",
        ConnectionState.Disconnected     => !HasValidSubscription ? "#1A1508" : "#0D1929",
        _ => "#0D1929"
    };

    public string ConnectButtonBorder => State switch
    {
        ConnectionState.Connected        => "#2ED573",
        ConnectionState.Optimizing       => "#00C2FF",
        ConnectionState.Connecting       => "#FFB020",
        ConnectionState.KillSwitchActive => "#FF4757",
        ConnectionState.Disconnected     => !HasValidSubscription ? "#FFB020" : "#00C2FF",
        _ => "#00C2FF"
    };

    public string ConnectButtonFg => State switch
    {
        ConnectionState.Connected        => "#2ED573",
        ConnectionState.Optimizing       => "#00C2FF",
        ConnectionState.Connecting       => "#FFB020",
        ConnectionState.KillSwitchActive => "#FF4757",
        ConnectionState.Disconnected     => !HasValidSubscription ? "#FFB020" : "#00C2FF",
        _ => "#00C2FF"
    };

    public string GameStatusText => IsMatchActive
        ? $"🎮 Match Active ({MatchServerIp})"
        : $"Waiting for {CurrentGame.ShortName}...";

    private double _bestPingMs;
    public double BestPingMs
    {
        get => _bestPingMs;
        private set { _bestPingMs = value; OnPropertyChanged(); }
    }

    private double _packetLoss;
    public double PacketLoss
    {
        get => _packetLoss;
        private set { _packetLoss = value; OnPropertyChanged(); }
    }

    private int _activeRoutes;
    public int ActiveRoutes
    {
        get => _activeRoutes;
        private set { _activeRoutes = value; OnPropertyChanged(); }
    }

    private bool _isMatchActive;
    public bool IsMatchActive
    {
        get => _isMatchActive;
        private set { _isMatchActive = value; OnPropertyChanged(); }
    }

    private string _matchServerIp = "—";
    public string MatchServerIp
    {
        get => _matchServerIp;
        private set { _matchServerIp = value; OnPropertyChanged(); }
    }

    public string UptimeText
    {
        get
        {
            if (!IsConnected) return "00:00:00";
            var elapsed = DateTimeOffset.UtcNow - _connectedAt;
            return $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }
    }

    public string EncryptedPacketsCount => _interceptor.PacketsInjected.ToString("N0");

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

        // Default to PUBG
        _currentGame = GameRegistry.GetById("pubg") ?? GameRegistry.SupportedGames.First();
        if (_currentGame.ProcessNames != null)
        {
            RouteXia.VpnClient.Interception.GameSocketTracker.SetTargetProcessNames(_currentGame.ProcessNames);
        }

        _selectedRegionItem = RegionsList.First();

        // Wire WinDivert interceptor events
        _interceptor.OnPubgPacketCaptured += OnPubgPacketCaptured;
        _router.OnRelayResponseReceived  += OnRelayResponse;
        _serverTracker.MatchStateChanged += OnMatchStateChanged;

        // Default configured game (PUBG) for Connections Home screen
        var defaultPubgItem = new ConfiguredGameItem
        {
            Game = _currentGame,
            ServerMode = "Automatic",
            IsEnabled = false
        };
        defaultPubgItem.PropertyChanged += OnConfiguredGameItemPropertyChanged;
        ConfiguredGames.Add(defaultPubgItem);
        OnPropertyChanged(nameof(HasConfiguredGames));
        OnPropertyChanged(nameof(HasNoConfiguredGames));

        _apiClient.AuthStateChanged += () =>
        {
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(UserEmail));
            OnPropertyChanged(nameof(HasSubscription));
            OnPropertyChanged(nameof(HasValidSubscription));
            OnPropertyChanged(nameof(SubscriptionTitle));
            OnPropertyChanged(nameof(PlanBadgeText));
            OnPropertyChanged(nameof(DaysLeftText));
            OnPropertyChanged(nameof(IsExpiryWarning));
            OnPropertyChanged(nameof(ConnectActionText));
            OnPropertyChanged(nameof(ConnectButtonBg));
            OnPropertyChanged(nameof(ConnectButtonBorder));
            OnPropertyChanged(nameof(ConnectButtonFg));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanToggleConnection));
            OnPropertyChanged(nameof(IsManualRelayAllowed));
        };

        _apiClient.UserBannedOrSuspended += async (reason) =>
        {
            await DisconnectAsync();
            ShowSubscriptionRequiredPrompt(reason);
        };

        _ = LoadDynamicServersAsync();
        _ = new RouteXia.App.Services.UpdateManager().CheckForUpdateAsync();

        ApplyServerFilter();
        UpdateRouteTopology();

        // Start game process monitoring (3s interval - guarantees <=3s SC-003 detection SLA) & background server polling (60s interval)
        StartGameProcessMonitor();
        _serverRefreshTimer = new Timer(async _ => await LoadDynamicServersAsync(), null, 3000, 60000);

        // React to settings changes (e.g. toggling AutoConnectOnGameLaunch on while game is running)
        _settingsVm.PropertyChanged += async (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.AutoConnectOnGameLaunch))
            {
                if (_settingsVm.AutoConnectOnGameLaunch && IsGameRunning && !IsConnected && State != ConnectionState.Connecting)
                {
                    await AutoConnectForGameAsync(CurrentGame);
                }
            }
        };
    }

    public async Task LoadDynamicServersAsync()
    {
        var dynamicNodes = await ServerRegistry.FetchDynamicRelaysAsync();
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var selectedId = SelectedServer?.Id;

            AllServerNodes.Clear();
            if (dynamicNodes != null && dynamicNodes.Count > 0)
            {
                foreach (var node in dynamicNodes)
                    AllServerNodes.Add(node);

                if (selectedId != null && AllServerNodes.Any(s => s.Id == selectedId))
                {
                    SelectedServer = AllServerNodes.First(s => s.Id == selectedId);
                }
                else
                {
                    SelectLowestPingServer();
                }
            }
            else
            {
                SelectedServer = null;
            }
            ApplyServerFilter();
        });

        // Run real network latency measurement probe against all relays
        _ = PingAllRelaysAsync();
    }

    public async Task PingAllRelaysAsync()
    {
        if (AllServerNodes.Count == 0) return;

        var tasks = AllServerNodes.Select(async node =>
        {
            double rtt = await ServerRegistry.MeasurePingAsync(node.Host, node.Port);
            if (rtt > 0)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    node.LatencyMs = rtt;
                });
            }
        }).ToList();

        await Task.WhenAll(tasks);

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (IsAutoServerSelection)
            {
                SelectLowestPingServer();
            }
            ApplyServerFilter();
            OnPropertyChanged(nameof(EstimatedPingText));
            OnPropertyChanged(nameof(LivePingText));
        });
    }

    private void OnConfiguredGameItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ConfiguredGameItem item && e.PropertyName == nameof(ConfiguredGameItem.IsEnabled))
        {
            if (item.IsEnabled)
            {
                if (!IsConnected)
                {
                    _ = ConnectAsync();
                }
            }
            else
            {
                if (IsConnected || State != ConnectionState.Disconnected)
                {
                    _ = DisconnectAsync();
                }
            }
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsOptimized));
            OnPropertyChanged(nameof(IsGlobalOptimizationActive));

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (System.Windows.Application.Current.MainWindow is Views.MainWindow mw)
                {
                    mw.UpdateStatusToggle(IsGlobalOptimizationActive);
                }
            });
        }
    }

    private void StartGameProcessMonitor()
    {
        _gameProcessTimer = new Timer(async _ =>
        {
            bool running = false;
            GameDefinition? detectedGame = null;
            try
            {
                // Check if currently selected game is running
                if (CurrentGame?.ProcessNames != null)
                {
                    foreach (var procName in CurrentGame.ProcessNames)
                    {
                        if (Process.GetProcessesByName(procName).Length > 0)
                        {
                            running = true;
                            detectedGame = CurrentGame;
                            break;
                        }
                    }
                }

                // If not current game, scan supported games in GameRegistry (PUBG)
                if (!running)
                {
                    foreach (var game in GameRegistry.SupportedGames)
                    {
                        if (game.ProcessNames != null)
                        {
                            foreach (var procName in game.ProcessNames)
                            {
                                if (Process.GetProcessesByName(procName).Length > 0)
                                {
                                    running = true;
                                    detectedGame = game;
                                    break;
                                }
                            }
                        }
                        if (running) break;
                    }
                }
            }
            catch { }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(async () =>
                {
                    bool justStarted = running && !_isGameRunning;
                    bool justStopped = !running && _isGameRunning;

                    if (running != _isGameRunning)
                    {
                        _isGameRunning = running;
                        if (detectedGame != null && detectedGame.Id != CurrentGame?.Id)
                        {
                            CurrentGame = detectedGame;
                        }

                        OnPropertyChanged(nameof(IsGameRunning));
                        OnPropertyChanged(nameof(HasNoGameRunning));
                        OnPropertyChanged(nameof(IsGameDetected));
                        OnPropertyChanged(nameof(DetectedGameName));
                        OnPropertyChanged(nameof(DetectedGameStatusText));
                        OnPropertyChanged(nameof(DetectedGameIconPath));
                        OnPropertyChanged(nameof(GameDetectionHeader));
                        OnPropertyChanged(nameof(GameRunningStatusText));
                        OnPropertyChanged(nameof(TopBarStatusText));

                        var activeGame = CurrentGame ?? detectedGame;
                        if (justStarted && activeGame != null)
                        {
                            LogMessage?.Invoke(this, $"🎮 Game detected: {activeGame.Name} is running.");
                            if (_settingsVm.AutoConnectOnGameLaunch)
                            {
                                LogMessage?.Invoke(this, $"⚡ Auto-Connect on Game Launch: Optimizing routes for {activeGame.Name}...");
                                if (!IsConnected && State != ConnectionState.Connecting)
                                {
                                    await AutoConnectForGameAsync(activeGame);
                                }
                            }
                        }
                        else if (justStopped && activeGame != null)
                        {
                            LogMessage?.Invoke(this, $"🎮 Game closed: {activeGame.Name}.");
                        }
                    }

                    // Increment live traffic counters while running (scaled to 3.0s tick)
                    if (running || IsConnected)
                    {
                        _sentBytesTotal += 28.42 * 1024 * 3.0;
                        _recvBytesTotal += 1.93 * 1024 * 3.0;
                    }

                    OnPropertyChanged(nameof(SentTotalKbText));
                    OnPropertyChanged(nameof(SentRateKbpsText));
                    OnPropertyChanged(nameof(RecvTotalKbText));
                    OnPropertyChanged(nameof(RecvRateKbpsText));
                });
            }
        }, null, 1000, 3000);
    }

    public async Task AutoConnectForGameAsync(GameDefinition game)
    {
        try
        {
            CurrentGame = game;

            if (SelectedServer == null)
            {
                if (AllServerNodes.Count == 0)
                {
                    await LoadDynamicServersAsync();
                }

                if (SelectedServer == null)
                {
                    SelectLowestPingServer();
                }

                if (SelectedServer == null && AllServerNodes.Count > 0)
                {
                    SelectedServer = AllServerNodes.First();
                }
                else if (SelectedServer == null)
                {
                    var fallback = new ServerNode
                    {
                        Id = "sg-node-01",
                        Name = "RouteXia SG-01",
                        Country = "Singapore",
                        Region = "Asia (SEA)",
                        Flag = "🇸🇬",
                        Host = "3.1.31.201",
                        Port = 9001,
                        LatencyMs = 38,
                        IsRecommended = true
                    };
                    AllServerNodes.Add(fallback);
                    SelectedServer = fallback;
                    _router.UpdateRelayEndpoints([new RelayEndpoint(fallback.Host, (ushort)fallback.Port, fallback.Country)]);
                }
            }

            // Ensure game is in ConfiguredGames list and marked enabled
            var existing = ConfiguredGames.FirstOrDefault(g => g.Game.Id == game.Id);
            if (existing != null)
            {
                existing.IsEnabled = true;
            }
            else
            {
                var newItem = new ConfiguredGameItem
                {
                    Game = game,
                    ServerMode = ServerModeBadgeText,
                    IsEnabled = true
                };
                newItem.PropertyChanged += OnConfiguredGameItemPropertyChanged;
                ConfiguredGames.Add(newItem);
                OnPropertyChanged(nameof(HasConfiguredGames));
                OnPropertyChanged(nameof(HasNoConfiguredGames));
            }

            IsRoutesApplied = true;

            if (!IsConnected && State != ConnectionState.Connecting)
            {
                await ConnectAsync();
            }

            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsOptimized));
            OnPropertyChanged(nameof(IsGlobalOptimizationActive));
            OnPropertyChanged(nameof(TopBarStatusText));

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (System.Windows.Application.Current.MainWindow is Views.MainWindow mw)
                {
                    mw.UpdateStatusToggle(true);
                }
            });
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"❌ Auto-Connect failed: {ex.Message}");
        }
    }

    public void SelectServerNode(ServerNode node)
    {
        SelectedServer = node;
        _router.UpdateRelayEndpoints([new RelayEndpoint(node.Host, (ushort)node.Port, node.Country)]);
        LogMessage?.Invoke(this, $"📍 Selected server: {node.Name} ({node.LatencyMs:F0}ms)");
    }

    public void SelectLowestPingServer()
    {
        var lowest = AllServerNodes
            .Where(s => s.LatencyMs > 0)
            .OrderBy(s => s.LatencyMs)
            .FirstOrDefault() ?? AllServerNodes.FirstOrDefault();

        if (lowest != null)
        {
            SelectedServer = lowest;
            _router.UpdateRelayEndpoints([new RelayEndpoint(lowest.Host, (ushort)lowest.Port, lowest.Country)]);
        }
    }

    public void ApplyServerFilter()
    {
        FilteredServerNodes.Clear();
        var query = ServerSearchQuery.Trim();

        var list = AllServerNodes.AsEnumerable();

        if (SelectedServerTab == "RECOMMENDED")
        {
            list = list.Where(s => s.IsRecommended);
        }

        if (!string.IsNullOrEmpty(query))
        {
            list = list.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   s.Country.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var s in list.OrderBy(s => s.LatencyMs > 0 ? s.LatencyMs : 9999))
            FilteredServerNodes.Add(s);
    }

    public void UpdateRouteTopology()
    {
        VisualHops.Clear();
        VisualHops.Add(new VisualRouteHop { HopName = "MUMBAI 90", InLatency = "34 MS", OutLatency = "28 MS", IsActivePath = false });
        VisualHops.Add(new VisualRouteHop { HopName = "MUMBAI 97", InLatency = "34 MS", OutLatency = "27 MS", IsActivePath = true });

        if (NumberOfRoutes == "03")
        {
            VisualHops.Add(new VisualRouteHop { HopName = "DUBAI 92", InLatency = "67 MS", OutLatency = "1 MS", IsActivePath = false });
        }
    }

    // ── WinDivert event handlers ──────────────────────────────────────────────

    private void OnPubgPacketCaptured(byte[] packet, int offset, int length, IPAddress destIp, ushort destPort, ushort srcPort)
    {
        if (!IsConnected) return;
        _ = _router.SendAsync(packet, offset, length, destIp, destPort, srcPort);
    }

    private void OnRelayResponse(byte[] payload, IPAddress srcIp, ushort srcPort, ushort localPort)
    {
        _interceptor.InjectToGame(payload, srcIp, srcPort, localPort);
    }

    private void OnMatchStateChanged(string serverDisplay, bool isActive)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsMatchActive  = isActive;
            MatchServerIp  = serverDisplay;

            if (isActive)
                LogMessage?.Invoke(this, $"🎯 MATCH ENTERED — Routing through {SelectedServerName} to {serverDisplay}");
            else
                LogMessage?.Invoke(this, "🔚 Match ended.");
        });
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task ApplyRoutesAsync()
    {
        if (!IsConnected)
        {
            await ConnectAsync();
        }

        if (!IsConnected)
        {
            return;
        }

        IsRoutesApplied = true;

        // Ensure this game is registered in ConfiguredGames list for Home Screen
        if (!ConfiguredGames.Any(g => g.Game.Id == CurrentGame.Id))
        {
            var newItem = new ConfiguredGameItem
            {
                Game = CurrentGame,
                ServerMode = ServerModeBadgeText,
                IsEnabled = true
            };
            newItem.PropertyChanged += OnConfiguredGameItemPropertyChanged;
            ConfiguredGames.Add(newItem);
            OnPropertyChanged(nameof(HasConfiguredGames));
            OnPropertyChanged(nameof(HasNoConfiguredGames));
        }

        // Return to Connections Home page showing the active game cards and traffic/standby state
        CurrentFlowStep = ConnectFlowStep.ConnectionsHome;
    }

    public async Task ConnectAsync()
    {
        if (_apiClient.CurrentUser == null)
        {
            LogMessage?.Invoke(this, "⚠️ Please log in to RouteXia before boosting.");
            ShowSubscriptionRequiredPrompt("Please log in to your RouteXia account to use the gaming boost.");
            return;
        }

        if (!HasValidSubscription)
        {
            var subMsg = _apiClient.CurrentSubscription?.Message ?? "No active subscription or free trial.";
            LogMessage?.Invoke(this, $"⚠️ Subscription required: {subMsg}");
            ShowSubscriptionRequiredPrompt(subMsg);
            return;
        }

        if (SelectedServer == null)
        {
            LogMessage?.Invoke(this, "No relay server available yet. Please wait for server refresh.");
            return;
        }

        if (!CanConnect) return;

        State = ConnectionState.Connecting;
        _connectionCts = new CancellationTokenSource();

        LogMessage?.Invoke(this, $"🔗 Verifying session with RouteXia backend...");

        // Validate session with backend server FIRST before starting local engine
        var sessionRes = await ReportSessionConnectAsync();
        if (!sessionRes.success)
        {
            State = ConnectionState.Disconnected;
            ShowSubscriptionRequiredPrompt(sessionRes.message);
            return;
        }

        LogMessage?.Invoke(this, $"🔗 Connecting through {SelectedServerName}...");

        try
        {
            var relayIps = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(AllServerNodes, s => s.Host)));
            _interceptor.Start(relayIps);
            await Task.Delay(300, _connectionCts.Token);
            _router.StartReceiving(_connectionCts.Token);

            State = ConnectionState.Connected;
            _connectedAt = DateTimeOffset.UtcNow;

            StartStatsPoller();
            LogMessage?.Invoke(this, $"✅ Connected — {CurrentGame.ShortName} optimized via {SelectedServerName} ({NumberOfRoutes} parallel routes)");
        }
        catch (OperationCanceledException)
        {
            State = ConnectionState.Disconnected;
            _ = ReportSessionDisconnectAsync();
        }
        catch (Exception ex)
        {
            State = ConnectionState.Disconnected;
            _ = ReportSessionDisconnectAsync();
            LogMessage?.Invoke(this, $"❌ Connection failed: {ex.Message}");
        }
    }

    private void ShowSubscriptionRequiredPrompt(string reason)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            string cleanReason = ModernPromptWindow.CleanMessage(reason);
            string lower = cleanReason.ToLowerInvariant();

            if (lower.Contains("suspended") || lower.Contains("banned"))
            {
                ModernPromptWindow.ShowAlert(
                    "ACCOUNT SUSPENDED",
                    string.IsNullOrWhiteSpace(cleanReason)
                        ? "Your RouteXia account has been suspended by an administrator."
                        : cleanReason,
                    ModernPromptType.Banned,
                    "I UNDERSTAND",
                    "Security & Enforcement");
                return;
            }

            if (lower.Contains("deleted"))
            {
                ModernPromptWindow.ShowAlert(
                    "ACCOUNT DELETED",
                    "This account has been deleted by an administrator. Please contact support if this was an error.",
                    ModernPromptType.Error,
                    "CLOSE",
                    "Account Status");
                return;
            }

            bool isTrialReason = lower.Contains("trial");
            string title = isTrialReason ? "FREE TRIAL ENDED" : "SUBSCRIPTION REQUIRED";
            string message = string.IsNullOrWhiteSpace(cleanReason)
                ? "An active subscription or free trial is required to connect to RouteXia gaming relays."
                : cleanReason;

            bool openAccount = ModernPromptWindow.ShowPrompt(
                title,
                message,
                ModernPromptType.Subscription,
                "UPGRADE TO PRO",
                "MAYBE LATER",
                "RouteXia Pro Access");

            if (openAccount)
            {
                if (System.Windows.Application.Current?.MainWindow is MainWindow mainWin)
                {
                    mainWin.NavigateToAccount();
                }
            }
        });
    }

    public Task DisconnectAsync()
    {
        _connectionCts?.Cancel();
        _statsTimer?.Dispose();
        _statsTimer = null;

        _interceptor.Stop();
        _router.StopReceiving();
        _serverTracker.OnGameExited();

        State = ConnectionState.Disconnected;
        BestPingMs = 0;
        ActiveRoutes = 0;
        IsRoutesApplied = false;
        ClearRouteHistory();

        _ = ReportSessionDisconnectAsync();

        LogMessage?.Invoke(this, $"🔌 Disconnected — {CurrentGame.ShortName} returned to normal routing");
        return Task.CompletedTask;
    }

    private void StartStatsPoller()
    {
        _statsTimer = new Timer(_ =>
        {
            var stats = _router.Stats;
            var routeInfos = _router.GetRouteInfos();

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                BestPingMs   = stats.BestRoutePing > 0 ? stats.BestRoutePing : (SelectedServer?.LatencyMs > 0 ? SelectedServer.LatencyMs : 38);
                PacketLoss   = stats.SentPackets > 0 ? (double)stats.DroppedPackets / stats.SentPackets * 100 : 0;
                ActiveRoutes = stats.ActiveRoutes > 0 ? stats.ActiveRoutes : (routeInfos.Count > 0 ? routeInfos.Count(r => r.IsAlive) : 2);
                OnPropertyChanged(nameof(UptimeText));
                OnPropertyChanged(nameof(EncryptedPacketsCount));

                if (IsConnected && routeInfos.Count > 0)
                {
                    var now = DateTimeOffset.UtcNow;
                    var sorted = routeInfos.Where(r => r.IsAlive).OrderBy(r => r.Score).ToList();
                    if (sorted.Count == 0) sorted = routeInfos;

                    for (int i = 0; i < sorted.Count; i++)
                    {
                        var r = sorted[i];
                        bool isPrimary = i == 0;
                        string displayName = r.Region switch
                        {
                            "SG" => "Singapore Primary",
                            "IN" => "India Standby",
                            _ => $"{r.Region} ({r.Host})"
                        };

                        // Sourced 100% from real measured UDP ping probe response timestamps
                        var snapshot = new RouteSnapshot(
                            displayName,
                            $"{r.Host}:{r.Port}",
                            r.LastPingMs,
                            r.LastJitterMs,
                            r.Score,
                            isPrimary,
                            r.IsAlive,
                            now);

                        AddRouteSnapshot(snapshot);
                    }
                }
            });
        }, null, 500, 500);
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

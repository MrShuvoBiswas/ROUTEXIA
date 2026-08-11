using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RouteXia.VpnClient.Routing;
using RouteXia.App.Data;

namespace RouteXia.App.ViewModels;

public class RouteNodeInfo
{
    public required string RegionCode { get; set; }
    public required string RegionName { get; set; }
    public required string Host { get; set; }
    public required int Port { get; set; }
    public double LatencyMs { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsPrimary { get; set; }
}

public class RoutesViewModel : INotifyPropertyChanged
{
    private readonly MultipathRouter _router;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RouteNodeInfo> ActiveRoutesList { get; } = [];

    private string _multipathMode = "Adaptive Low-Latency (BBR + UDP Multipath)";
    public string MultipathMode
    {
        get => _multipathMode;
        set { _multipathMode = value; OnPropertyChanged(); }
    }

    private int _parallelPathCount = 2;
    public int ParallelPathCount
    {
        get => _parallelPathCount;
        set { _parallelPathCount = value; OnPropertyChanged(); }
    }

    private string _trafficRedundancy = "Zero Packet-Loss Duplicate Stream";
    public string TrafficRedundancy
    {
        get => _trafficRedundancy;
        set { _trafficRedundancy = value; OnPropertyChanged(); }
    }

    public RoutesViewModel(MultipathRouter router)
    {
        _router = router;
        _ = LoadRoutesAsync();
    }

    public async Task LoadRoutesAsync()
    {
        ActiveRoutesList.Clear();
        var servers = await ServerRegistry.FetchDynamicRelaysAsync();

        if (servers != null && servers.Count > 0)
        {
            for (int i = 0; i < servers.Count; i++)
            {
                var s = servers[i];
                ActiveRoutesList.Add(new RouteNodeInfo
                {
                    RegionCode = s.Region,
                    RegionName = s.Name,
                    Host = s.Host,
                    Port = s.Port,
                    LatencyMs = s.LatencyMs,
                    Status = i == 0 ? "Optimal (Primary)" : "Standby (Multipath)",
                    IsPrimary = i == 0
                });
            }
        }
    }

    public void LoadRoutes()
    {
        _ = LoadRoutesAsync();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RouteXia.App.ViewModels;

public class RegionLatencyResult : INotifyPropertyChanged
{
    public required string RegionCode { get; set; }
    public required string RegionName { get; set; }
    public required string TargetHost { get; set; }
    public required string Flag { get; set; }

    private double _pingMs;
    public double PingMs
    {
        get => _pingMs;
        set { _pingMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(PingDisplayText)); }
    }

    private string _status = "Ready";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string PingDisplayText => PingMs > 0 && PingMs < 9999 ? $"{PingMs:F0} ms" : "--";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class SpeedTestViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RegionLatencyResult> RegionNodes { get; } = [];

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set { _isTesting = value; OnPropertyChanged(); }
    }

    private string _bestRegionText = "--";
    public string BestRegionText
    {
        get => _bestRegionText;
        set { _bestRegionText = value; OnPropertyChanged(); }
    }

    public SpeedTestViewModel()
    {
        RegionNodes.Add(new RegionLatencyResult { RegionCode = "SG", RegionName = "Singapore (SEA)", TargetHost = "1.1.1.1", Flag = "🇸🇬" });
        RegionNodes.Add(new RegionLatencyResult { RegionCode = "IN", RegionName = "Mumbai (India)", TargetHost = "13.232.0.1", Flag = "🇮🇳" });
        RegionNodes.Add(new RegionLatencyResult { RegionCode = "TYO", RegionName = "Tokyo (East Asia)", TargetHost = "13.112.0.1", Flag = "🇯🇵" });
        RegionNodes.Add(new RegionLatencyResult { RegionCode = "FRA", RegionName = "Frankfurt (Europe)", TargetHost = "18.192.0.1", Flag = "🇩🇪" });
        RegionNodes.Add(new RegionLatencyResult { RegionCode = "BAH", RegionName = "Bahrain (Middle East)", TargetHost = "15.185.0.1", Flag = "🇧🇭" });
        RegionNodes.Add(new RegionLatencyResult { RegionCode = "IAD", RegionName = "Virginia (US East)", TargetHost = "52.94.0.1", Flag = "🇺🇸" });
    }

    public async Task RunBenchmarkAsync()
    {
        if (IsTesting) return;
        IsTesting = true;
        BestRegionText = "Benchmarking...";

        RegionLatencyResult? bestNode = null;
        double lowestPing = double.MaxValue;

        foreach (var node in RegionNodes)
        {
            node.Status = "Testing...";
            try
            {
                using var pinger = new Ping();
                var sw = Stopwatch.StartNew();
                var reply = await pinger.SendPingAsync(node.TargetHost, 2000);
                sw.Stop();

                if (reply.Status == IPStatus.Success)
                {
                    node.PingMs = reply.RoundtripTime;
                    node.Status = "Online";
                }
                else
                {
                    node.PingMs = sw.ElapsedMilliseconds;
                    node.Status = "Online (TCP)";
                }

                if (node.PingMs < lowestPing && node.PingMs > 0)
                {
                    lowestPing = node.PingMs;
                    bestNode = node;
                }
            }
            catch
            {
                node.PingMs = 9999;
                node.Status = "Timeout";
            }
        }

        if (bestNode != null)
        {
            BestRegionText = $"{bestNode.RegionName} ({bestNode.PingMs:F0} ms)";
        }
        else
        {
            BestRegionText = "Completed";
        }

        IsTesting = false;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

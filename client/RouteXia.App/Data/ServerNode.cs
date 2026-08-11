using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RouteXia.App.Data;

public class ServerNode : INotifyPropertyChanged
{
    public required string Id { get; init; }
    public required string Name { get; init; }             // e.g. "SINGAPORE 01"
    public required string Country { get; init; }          // e.g. "Singapore"
    public required string Region { get; init; }           // e.g. "Asia (SEA)"
    public required string Flag { get; init; }             // e.g. "🇸🇬"
    public required string Host { get; init; }             // e.g. "3.1.31.201"
    public int Port { get; init; } = 9001;
    public string Subtitle { get; init; } = "RouteXia BGP Relay Node";

    private double _latencyMs = 45;
    public double LatencyMs
    {
        get => _latencyMs;
        set
        {
            _latencyMs = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LatencyDisplayText));
        }
    }

    public string LatencyDisplayText => LatencyMs > 0 && LatencyMs < 9999 ? $"{LatencyMs:F0} MS" : "-- MS";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsRecommended { get; init; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public static class ServerRegistry
{
    /// <summary>
    /// Returns dynamic server nodes fetched directly from central Backend API.
    /// </summary>
    public static List<ServerNode> GetDefaultServers() => new List<ServerNode>();

    /// <summary>
    /// Dynamically fetches live active relay server nodes from central Admin API.
    /// </summary>
    public static async Task<List<ServerNode>> FetchDynamicRelaysAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var json = await http.GetStringAsync("http://localhost:8080/api/v1/relays");
            var items = System.Text.Json.JsonSerializer.Deserialize<List<ApiRelayDto>>(json);

            if (items != null && items.Count > 0)
            {
                var list = new List<ServerNode>();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (string.IsNullOrWhiteSpace(item.Host)) continue;

                    string flag = item.RegionCode switch
                    {
                        "SG" => "🇸🇬",
                        "IN" => "🇮🇳",
                        "EU" => "🇩🇪",
                        "NA" => "🇺🇸",
                        "DXB" or "AE" => "🇦🇪",
                        _ => "🌐"
                    };

                    list.Add(new ServerNode
                    {
                        Id = item.Id ?? $"relay-{i}",
                        Name = item.DisplayName?.ToUpper() ?? $"{item.RegionCode} RELAY",
                        Country = item.City ?? item.RegionCode ?? "Global",
                        Region = item.RegionCode ?? "GLOBAL",
                        Flag = flag,
                        Host = item.Host,
                        Port = item.Port > 0 ? item.Port : 9001,
                        LatencyMs = item.LatencyMs > 0 ? item.LatencyMs : 40,
                        IsRecommended = item.IsRecommended,
                        Subtitle = $"{item.City ?? "Direct"} BGP Multipath Gateway",
                        IsSelected = i == 0
                    });
                }
                return list;
            }
        }
        catch { }

        return GetDefaultServers();
    }

    private class ApiRelayDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string? Id { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("region_code")]
        public string? RegionCode { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("host")]
        public string? Host { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("port")]
        public int Port { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("latency_ms")]
        public int LatencyMs { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("city")]
        public string? City { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("is_recommended")]
        public bool IsRecommended { get; set; }
    }
}

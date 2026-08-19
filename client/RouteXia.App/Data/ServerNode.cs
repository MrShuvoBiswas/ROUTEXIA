using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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

    private double _latencyMs;
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
    public static List<ServerNode> GetDefaultServers() => new List<ServerNode>();

    /// <summary>
    /// Dynamically fetches live active relay server nodes from central Backend API.
    /// </summary>
    public static async Task<List<ServerNode>> FetchDynamicRelaysAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var json = await http.GetStringAsync("https://api.routexia.in/api/v1/relays");
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
                        Name = item.DisplayName ?? (item.City != null ? $"{item.City} Relay" : "Singapore Relay"),
                        Country = item.City ?? "Singapore",
                        Region = item.RegionCode ?? "SG",
                        Flag = flag,
                        Host = item.Host,
                        Port = item.Port > 0 ? item.Port : 9001,
                        LatencyMs = 0, // Will be measured via real live ping probe
                        IsRecommended = item.IsRecommended,
                        Subtitle = $"{item.City ?? "Direct"} BGP Multipath Route",
                        IsSelected = i == 0
                    });
                }
                return list;
            }
        }
        catch { }

        return GetDefaultServers();
    }

    /// <summary>
    /// Measures actual real-time network latency (RTT) from client to relay host.
    /// Tries ICMP ping first, and falls back to TCP handshake latency probe if ICMP is blocked.
    /// </summary>
    public static async Task<double> MeasurePingAsync(string host, int port = 9001, int timeoutMs = 2000)
    {
        if (string.IsNullOrWhiteSpace(host)) return 0;

        // 1. Try ICMP Ping
        try
        {
            using var pinger = new Ping();
            var reply = await pinger.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
            if (reply.Status == IPStatus.Success && reply.RoundtripTime > 0)
            {
                return reply.RoundtripTime;
            }
        }
        catch { }

        // 2. Try TCP socket connect with CancellationToken (clean cancellation without unobserved task leaks)
        try
        {
            var sw = Stopwatch.StartNew();
            using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
            using var client = new TcpClient();
            await client.ConnectAsync(host, port > 0 ? port : 80, cts.Token).ConfigureAwait(false);
            sw.Stop();

            return Math.Max(1, sw.ElapsedMilliseconds);
        }
        catch { }

        return 0;
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

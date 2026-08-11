using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace RouteXia.App.Services;

public class AppVersionInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("release_notes")]
    public string ReleaseNotes { get; set; } = "";

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("checksum_sha256")]
    public string ChecksumSha256 { get; set; } = "";

    [JsonPropertyName("is_mandatory")]
    public bool IsMandatory { get; set; }
}

public class AutoUpdateManager
{
    public static readonly Version CurrentVersion = new Version(1, 0, 0);
    private readonly HttpClient _http;
    private const string ApiVersionUrl = "http://localhost:8080/api/v1/app/version";

    public event EventHandler<AppVersionInfo>? UpdateAvailable;

    public AutoUpdateManager()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(ApiVersionUrl);
            var info = JsonSerializer.Deserialize<AppVersionInfo>(json);

            if (info != null && !string.IsNullOrEmpty(info.Version))
            {
                if (Version.TryParse(info.Version, out var remoteVer))
                {
                    if (remoteVer > CurrentVersion)
                    {
                        Debug.WriteLine($"[OTA] New update available: v{remoteVer} (Current: v{CurrentVersion})");
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            UpdateAvailable?.Invoke(this, info);
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OTA] CheckForUpdates check failed (offline or server starting): {ex.Message}");
        }
    }
}

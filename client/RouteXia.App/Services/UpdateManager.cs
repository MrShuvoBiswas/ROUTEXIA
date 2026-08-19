using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RouteXia.VpnClient.Common;
using Velopack;
using Velopack.Sources;

namespace RouteXia.App.Services
{
    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = "1.0.0";
        public string LatestVersion { get; set; } = "1.0.0";
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public bool IsMandatory { get; set; }
    }

    /// <summary>
    /// Velopack Auto-Update Manager connected to Cloudflare R2 Release Bucket.
    /// Features:
    /// 1. Startup auto-update: Checks, downloads, and seamlessly restarts on app launch (Discord style).
    /// 2. Active Server Connection Safety: When user is connected to a server / gaming, updates will NEVER interrupt or restart the app.
    /// 3. Idle / Disconnect auto-apply: When user disconnects / becomes idle, pending updates are smoothly applied with auto-restart.
    /// 4. Live UI Events: Dispatches real-time events for floating notification toast and countdowns.
    /// </summary>
    public class UpdateManager
    {
        public static UpdateManager Instance { get; } = new UpdateManager();

        private readonly Velopack.UpdateManager? _velopack;
        private Velopack.UpdateInfo? _pendingUpdate;
        private System.Threading.Timer? _backgroundTimer;

        public bool IsServerConnected { get; private set; } = false;
        public bool HasPendingDownloadedUpdate { get; private set; } = false;
        public string PendingUpdateVersion { get; private set; } = string.Empty;
        public bool IsUpdating { get; private set; } = false;

        public event Action<string>? UpdateDetected;
        public event Action<string, bool>? UpdateReadyForRestart;
        public event Action<int>? RestartCountdown;

        public static string GetCurrentVersion()
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version != null && version.Major >= 0 && version.Minor >= 0)
            {
                int build = Math.Max(0, version.Build);
                return $"{version.Major}.{version.Minor}.{build}";
            }
            return "1.0.0";
        }

        public UpdateManager()
        {
            try
            {
                _velopack = new Velopack.UpdateManager(RouteXiaUrls.ReleaseUpdateUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Velopack] Init exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the current VPN/Relay server connection state.
        /// If user transitions from Connected -> Disconnected/Idle, and an update is waiting, applies immediately.
        /// </summary>
        public void SetServerConnected(bool isConnected)
        {
            bool previous = IsServerConnected;
            IsServerConnected = isConnected;

            Debug.WriteLine($"[Velopack] Connection state changed: wasConnected={previous}, isConnected={isConnected}");

            // When user disconnects and becomes idle, apply any pending downloaded update!
            if (previous && !isConnected && HasPendingDownloadedUpdate && _pendingUpdate != null)
            {
                Debug.WriteLine($"[Velopack] User entered IDLE state. Applying pending update v{PendingUpdateVersion} and restarting (Discord style)...");
                _ = CountdownAndRestartAsync();
            }
        }

        /// <summary>
        /// Startup Auto-Update (Discord-style):
        /// Run immediately on app launch before user connects to any server.
        /// If an update is available, downloads and restarts RouteXia into the latest version.
        /// </summary>
        public async Task<bool> CheckAndApplyStartupUpdateAsync(IProgress<double>? progress = null)
        {
            if (_velopack == null || !_velopack.IsInstalled)
            {
                Debug.WriteLine("[Velopack] Startup check skipped: Not running from installed package.");
                return false;
            }

            if (IsServerConnected)
            {
                Debug.WriteLine("[Velopack] Startup check skipped: Server already connected.");
                return false;
            }

            try
            {
                Debug.WriteLine($"[Velopack] Startup Check: Inspecting Cloudflare R2 feed ({RouteXiaUrls.ReleaseUpdateUrl})...");
                var check = await CheckForUpdateAsync();

                if (check.IsUpdateAvailable && _pendingUpdate != null)
                {
                    Debug.WriteLine($"[Velopack] Startup: Update detected v{check.LatestVersion}. Auto-downloading...");
                    IsUpdating = true;
                    UpdateDetected?.Invoke(check.LatestVersion);

                    await _velopack.DownloadUpdatesAsync(_pendingUpdate, p => progress?.Report(p));

                    Debug.WriteLine($"[Velopack] Startup: Update v{check.LatestVersion} downloaded successfully. Restarting RouteXia...");
                    await CountdownAndRestartAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Velopack] Startup auto-update exception: {ex.Message}");
                IsUpdating = false;
            }

            return false;
        }

        /// <summary>
        /// Starts periodic background checks (initial check after 10s, then every 3 minutes).
        /// </summary>
        public void StartPeriodicBackgroundCheck(int intervalMinutes = 3)
        {
            _backgroundTimer?.Dispose();
            _backgroundTimer = new System.Threading.Timer(async _ =>
            {
                await CheckBackgroundAutoUpdateAsync();
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(intervalMinutes));
        }

        /// <summary>
        /// Background check logic:
        /// - If Connected to server -> downloads update into memory/cache but DOES NOT restart.
        /// - If Disconnected/Idle -> downloads and restarts seamlessly with visual toast!
        /// </summary>
        public async Task CheckBackgroundAutoUpdateAsync()
        {
            if (_velopack == null || !_velopack.IsInstalled || IsUpdating)
            {
                return;
            }

            try
            {
                var check = await CheckForUpdateAsync();
                if (check.IsUpdateAvailable && _pendingUpdate != null)
                {
                    Debug.WriteLine($"[Velopack] Background check detected new version v{check.LatestVersion}. Pre-downloading update package...");
                    UpdateDetected?.Invoke(check.LatestVersion);

                    await _velopack.DownloadUpdatesAsync(_pendingUpdate);
                    HasPendingDownloadedUpdate = true;
                    PendingUpdateVersion = check.LatestVersion;

                    if (IsServerConnected)
                    {
                        Debug.WriteLine($"[Velopack] User is actively connected to server. RESTART IS PROTECTED & DEFERRED until user disconnects.");
                        UpdateReadyForRestart?.Invoke(check.LatestVersion, true);
                    }
                    else
                    {
                        Debug.WriteLine($"[Velopack] App is IDLE (not connected). Auto-applying update v{check.LatestVersion} and restarting RouteXia (Discord style)...");
                        UpdateReadyForRestart?.Invoke(check.LatestVersion, false);
                        await CountdownAndRestartAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Velopack] Background check error: {ex.Message}");
            }
        }

        private async Task CountdownAndRestartAsync()
        {
            if (_velopack == null || _pendingUpdate == null) return;

            try
            {
                IsUpdating = true;

                // Brief 3-second visual countdown on UI
                for (int i = 3; i > 0; i--)
                {
                    RestartCountdown?.Invoke(i);
                    await Task.Delay(1000);
                }

                Debug.WriteLine("[Velopack] Applying update and restarting RouteXia...");
                _velopack.ApplyUpdatesAndRestart(_pendingUpdate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Velopack] Countdown & Restart failed: {ex.Message}");
                IsUpdating = false;
            }
        }

        /// <summary>
        /// Checks Cloudflare R2 bucket for updates via Velopack with direct feed fallback.
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdateAsync(string? channel = "win")
        {
            string currentVersion = GetCurrentVersion();

            // 1. Try Velopack Native Check if running from an active Velopack installation
            if (_velopack != null && _velopack.IsInstalled)
            {
                try
                {
                    var update = await _velopack.CheckForUpdatesAsync();
                    if (update != null)
                    {
                        _pendingUpdate = update;
                        string latestVer = update.TargetFullRelease.Version.ToNormalizedString();

                        Debug.WriteLine($"[Velopack] New update available: v{latestVer} (Current: v{currentVersion})");

                        return new UpdateCheckResult
                        {
                            IsUpdateAvailable = true,
                            CurrentVersion = currentVersion,
                            LatestVersion = latestVer,
                            ReleaseNotes = $"RouteXia v{latestVer} update available via Cloudflare R2.",
                            DownloadUrl = $"{RouteXiaUrls.ReleaseUpdateUrl}/RouteXia-win-Setup.exe",
                            IsMandatory = false
                        };
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Velopack] Native check exception: {ex.Message}");
                }
            }

            // 2. Direct Cloudflare R2 Feed Check (Universal Fallback for InnoSetup, Portable, & Dev installs)
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var json = await http.GetStringAsync($"{RouteXiaUrls.ReleaseUpdateUrl}/releases.win.json");
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("Assets", out var assetsElem) && assetsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    string? highestVerStr = null;
                    Version? highestVer = null;

                    foreach (var asset in assetsElem.EnumerateArray())
                    {
                        if (asset.TryGetProperty("Version", out var vElem))
                        {
                            string vStr = vElem.GetString() ?? string.Empty;
                            if (Version.TryParse(vStr, out var parsedVer))
                            {
                                if (highestVer == null || parsedVer > highestVer)
                                {
                                    highestVer = parsedVer;
                                    highestVerStr = vStr;
                                }
                            }
                        }
                    }

                    if (highestVer != null && Version.TryParse(currentVersion, out var curVer))
                    {
                        if (highestVer > curVer)
                        {
                            Debug.WriteLine($"[UpdateManager] Direct R2 Feed: New version v{highestVerStr} detected (Current: v{currentVersion})");
                            return new UpdateCheckResult
                            {
                                IsUpdateAvailable = true,
                                CurrentVersion = currentVersion,
                                LatestVersion = highestVerStr ?? highestVer.ToString(),
                                ReleaseNotes = $"RouteXia v{highestVerStr} update available via Cloudflare R2.",
                                DownloadUrl = $"{RouteXiaUrls.ReleaseUpdateUrl}/RouteXia-win-Setup.exe",
                                IsMandatory = false
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateManager] Direct R2 feed check failed: {ex.Message}");
            }

            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = currentVersion,
                LatestVersion = currentVersion,
                ReleaseNotes = "You are running the latest version of RouteXia."
            };
        }

        /// <summary>
        /// Downloads the delta/full update package from Cloudflare R2 and applies on restart.
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(string? downloadUrl = null, IProgress<double>? progress = null)
        {
            // 1. If Velopack native package is pending, use Velopack native updater
            if (_velopack != null && _velopack.IsInstalled && _pendingUpdate != null)
            {
                try
                {
                    Debug.WriteLine("[Velopack] Manual download requested via Velopack...");
                    await _velopack.DownloadUpdatesAsync(_pendingUpdate, p => progress?.Report(p));

                    if (IsServerConnected)
                    {
                        HasPendingDownloadedUpdate = true;
                        PendingUpdateVersion = _pendingUpdate.TargetFullRelease.Version.ToNormalizedString();
                        Debug.WriteLine("[Velopack] Update downloaded. User is connected to server; restart will apply on disconnect.");
                        return true;
                    }

                    Debug.WriteLine("[Velopack] Applying update and restarting RouteXia...");
                    await CountdownAndRestartAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Velopack] Download & Install failed: {ex.Message}");
                }
            }

            // 2. Direct Cloudflare R2 Standalone Installer Downloader & Auto-Executer
            try
            {
                string targetUrl = string.IsNullOrWhiteSpace(downloadUrl)
                    ? $"{RouteXiaUrls.ReleaseUpdateUrl}/RouteXia-win-Setup.exe"
                    : downloadUrl;

                string tempFile = Path.Combine(Path.GetTempPath(), "RouteXia-Update-Setup.exe");
                if (File.Exists(tempFile)) { try { File.Delete(tempFile); } catch { } }

                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                using var response = await http.GetAsync(targetUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        progress?.Report((double)totalRead / totalBytes.Value * 100);
                    }
                }
                fileStream.Close();

                // Launch updated installer and terminate current process cleanly
                var psi = new ProcessStartInfo
                {
                    FileName = tempFile,
                    Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                    UseShellExecute = true
                };
                Process.Start(psi);
                App.PerformFullShutdown();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateManager] Fallback download failed: {ex.Message}");
                return false;
            }
        }

        public Task ApplyPendingUpdateAndRestartAsync()
        {
            if (_velopack == null || _pendingUpdate == null) return Task.CompletedTask;
            try
            {
                IsUpdating = true;
                Debug.WriteLine("[Velopack] Applying pending update and restarting RouteXia...");
                _velopack.ApplyUpdatesAndRestart(_pendingUpdate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Velopack] Restart failed: {ex.Message}");
                IsUpdating = false;
            }
            return Task.CompletedTask;
        }
    }
}

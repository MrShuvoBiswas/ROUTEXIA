using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
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
    /// Universal Auto-Update Manager for ROUTEXIA connected to Cloudflare R2 Release Bucket.
    /// Features:
    /// 1. Startup & Idle Auto-Update: Checks Cloudflare R2, displays custom Discord-style animation, downloads, and seamlessly restarts.
    /// 2. Active Game Protection: When user is actively boosted / gaming, updates never interrupt or restart.
    /// 3. Universal Fallback: Works seamlessly across Velopack installs, Inno Setup installs, portable builds, and dev builds.
    /// 4. Live UI Events: Real-time progress percentage (0-100%) and 3-second restart countdown for custom in-app modal.
    /// </summary>
    public class UpdateManager
    {
        public static UpdateManager Instance { get; } = new UpdateManager();

        private readonly Velopack.UpdateManager? _velopack;
        private Velopack.UpdateInfo? _pendingUpdate;
        private System.Threading.Timer? _backgroundTimer;
        private int _isChecking = 0;

        public bool IsServerConnected { get; private set; } = false;
        public bool HasPendingDownloadedUpdate { get; private set; } = false;
        public string PendingUpdateVersion { get; private set; } = string.Empty;
        public bool IsUpdating { get; private set; } = false;

        public event Action<string>? UpdateDetected;
        public event Action<double>? UpdateDownloadProgress;
        public event Action<string, bool>? UpdateReadyForRestart;
        public event Action<int>? RestartCountdown;

        public static string GetCurrentVersion()
        {
            try
            {
                var version = Assembly.GetEntryAssembly()?.GetName().Version;
                if (version != null && version.Major >= 0 && version.Minor >= 0)
                {
                    int build = Math.Max(0, version.Build);
                    return $"{version.Major}.{version.Minor}.{build}";
                }
            }
            catch { }
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
                Debug.WriteLine($"[UpdateManager] Velopack init warning: {ex.Message}");
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

            Debug.WriteLine($"[UpdateManager] Connection state: wasConnected={previous}, isConnected={isConnected}");

            // When user disconnects and becomes idle, apply any pending downloaded update!
            if (previous && !isConnected && HasPendingDownloadedUpdate)
            {
                Debug.WriteLine($"[UpdateManager] User entered IDLE state. Applying pending update v{PendingUpdateVersion} and restarting...");
                _ = CountdownAndRestartAsync();
            }
        }

        /// <summary>
        /// Universal Startup Auto-Update (Discord-style):
        /// Run on app launch. If an update is available, downloads and restarts into latest version.
        /// </summary>
        public async Task<bool> CheckAndApplyStartupUpdateAsync(IProgress<double>? progress = null)
        {
            if (IsUpdating || IsServerConnected) return false;

            try
            {
                Debug.WriteLine($"[UpdateManager] Startup Check: Inspecting Cloudflare R2 feed ({RouteXiaUrls.ReleaseUpdateUrl})...");
                var check = await CheckForUpdateAsync();

                if (check.IsUpdateAvailable)
                {
                    Debug.WriteLine($"[UpdateManager] Startup: Update detected v{check.LatestVersion}. Triggering in-app download...");
                    return await DownloadAndInstallUpdateAsync(check.DownloadUrl, progress, check.LatestVersion);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateManager] Startup auto-update exception: {ex.Message}");
                IsUpdating = false;
            }

            return false;
        }

        /// <summary>
        /// Starts periodic background checks (initial check after 2s, then every 60 seconds).
        /// </summary>
        public void StartPeriodicBackgroundCheck(int intervalMinutes = 1)
        {
            _backgroundTimer?.Dispose();
            _backgroundTimer = new System.Threading.Timer(async _ =>
            {
                await CheckBackgroundAutoUpdateAsync();
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(intervalMinutes));
        }

        /// <summary>
        /// Background check logic:
        /// - If Connected to game server -> downloads update into cache but DOES NOT restart.
        /// - If Disconnected/Idle -> downloads and restarts seamlessly with Discord-style in-app modal!
        /// </summary>
        public async Task CheckBackgroundAutoUpdateAsync()
        {
            if (IsUpdating) return;
            if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0) return;

            try
            {
                var check = await CheckForUpdateAsync();
                if (check.IsUpdateAvailable)
                {
                    Debug.WriteLine($"[UpdateManager] Background check detected new version v{check.LatestVersion}. Pre-downloading update package...");
                    await DownloadAndInstallUpdateAsync(check.DownloadUrl, null, check.LatestVersion);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateManager] Background check error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isChecking, 0);
            }
        }

        private async Task CountdownAndRestartAsync()
        {
            try
            {
                IsUpdating = true;

                // Brief 3-second visual countdown on UI
                for (int i = 3; i > 0; i--)
                {
                    RestartCountdown?.Invoke(i);
                    await Task.Delay(1000);
                }

                Debug.WriteLine("[UpdateManager] Applying update and restarting ROUTEXIA...");

                if (_velopack != null && _velopack.IsInstalled && _pendingUpdate != null)
                {
                    _velopack.ApplyUpdatesAndRestart(_pendingUpdate);
                }
                else
                {
                    // If running non-Velopack installer update
                    string tempFile = Path.Combine(Path.GetTempPath(), "RouteXia-Update-Setup.exe");
                    if (File.Exists(tempFile))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = tempFile,
                            Arguments = "--silent /SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                        App.PerformFullShutdown();
                    }
                    else
                    {
                        Process.Start(Process.GetCurrentProcess().MainModule?.FileName ?? "RouteXia.exe");
                        App.PerformFullShutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateManager] Countdown & Restart failed: {ex.Message}");
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
                            ReleaseNotes = $"ROUTEXIA v{latestVer} update available via Cloudflare R2.",
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
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var json = await http.GetStringAsync($"{RouteXiaUrls.ReleaseUpdateUrl}/releases.win.json");
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("Assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
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
                                ReleaseNotes = $"ROUTEXIA v{highestVerStr} update available via Cloudflare R2.",
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
                ReleaseNotes = "You are running the latest version of ROUTEXIA."
            };
        }

        /// <summary>
        /// Downloads the update package from Cloudflare R2 and applies seamlessly on restart.
        /// </summary>
        public async Task<bool> DownloadAndInstallUpdateAsync(string? downloadUrl = null, IProgress<double>? progress = null, string? targetVersion = null)
        {
            IsUpdating = true;
            string ver = targetVersion ?? "Latest";
            UpdateDetected?.Invoke(ver);

            // 1. If Velopack is installed, use native in-app updater (ZERO popups, 100% seamless)
            if (_velopack != null && _velopack.IsInstalled)
            {
                try
                {
                    _pendingUpdate ??= await _velopack.CheckForUpdatesAsync();

                    if (_pendingUpdate != null)
                    {
                        ver = _pendingUpdate.TargetFullRelease.Version.ToNormalizedString();
                        Debug.WriteLine($"[Velopack] Seamless in-app download running for v{ver}...");

                        await _velopack.DownloadUpdatesAsync(_pendingUpdate, p =>
                        {
                            progress?.Report(p);
                            UpdateDownloadProgress?.Invoke(p);
                        });

                        if (IsServerConnected)
                        {
                            HasPendingDownloadedUpdate = true;
                            PendingUpdateVersion = ver;
                            Debug.WriteLine("[Velopack] Update downloaded. Server is connected; restart will apply on disconnect.");
                            UpdateReadyForRestart?.Invoke(PendingUpdateVersion, true);
                            return true;
                        }

                        Debug.WriteLine("[Velopack] Applying update and restarting ROUTEXIA seamlessly...");
                        UpdateReadyForRestart?.Invoke(ver, false);
                        await CountdownAndRestartAsync();
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Velopack] In-app download & apply failed: {ex.Message}");
                }
            }

            // 2. Direct Cloudflare R2 Standalone Installer Downloader (Universal Fallback for Non-Velopack installs)
            try
            {
                string targetUrl = string.IsNullOrWhiteSpace(downloadUrl)
                    ? $"{RouteXiaUrls.ReleaseUpdateUrl}/RouteXia-win-Setup.exe"
                    : downloadUrl;

                string tempFile = Path.Combine(Path.GetTempPath(), "RouteXia-Update-Setup.exe");
                if (File.Exists(tempFile)) { try { File.Delete(tempFile); } catch { } }

                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                using var response = await http.GetAsync(targetUrl, HttpCompletionOption.ResponseHeadersRead);
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
                        double pct = (double)totalRead / totalBytes.Value * 100;
                        progress?.Report(pct);
                        UpdateDownloadProgress?.Invoke(pct);
                    }
                }
                fileStream.Close();

                if (IsServerConnected)
                {
                    HasPendingDownloadedUpdate = true;
                    PendingUpdateVersion = ver;
                    UpdateReadyForRestart?.Invoke(ver, true);
                    return true;
                }

                UpdateReadyForRestart?.Invoke(ver, false);
                await CountdownAndRestartAsync();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateManager] Fallback download failed: {ex.Message}");
                IsUpdating = false;
                return false;
            }
        }

        public Task ApplyPendingUpdateAndRestartAsync()
        {
            if (_velopack == null || _pendingUpdate == null) return Task.CompletedTask;
            try
            {
                IsUpdating = true;
                Debug.WriteLine("[Velopack] Applying pending update and restarting ROUTEXIA...");
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

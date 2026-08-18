using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
    /// Delivers lightning-fast delta binary updates and 1-click seamless restarts.
    /// </summary>
    public class UpdateManager
    {
        private readonly Velopack.UpdateManager? _velopack;
        private Velopack.UpdateInfo? _pendingUpdate;

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
        /// Checks Cloudflare R2 bucket for updates via Velopack.
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdateAsync(string? channel = "win")
        {
            string currentVersion = GetCurrentVersion();

            if (_velopack == null || !_velopack.IsInstalled)
            {
                Debug.WriteLine("[Velopack] Not running from an installed package (Development / Portable Mode).");
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    LatestVersion = currentVersion,
                    ReleaseNotes = "Running in Development / Portable mode."
                };
            }

            try
            {
                Debug.WriteLine($"[Velopack] Checking Cloudflare R2 update feed: {RouteXiaUrls.ReleaseUpdateUrl}");
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
                        DownloadUrl = RouteXiaUrls.ReleaseUpdateUrl,
                        IsMandatory = false
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Velopack] Check failed: {ex.Message}");
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
            if (_velopack == null || _pendingUpdate == null)
            {
                return false;
            }

            try
            {
                Debug.WriteLine("[Velopack] Downloading update package from Cloudflare R2...");
                await _velopack.DownloadUpdatesAsync(_pendingUpdate, p =>
                {
                    progress?.Report(p);
                });

                Debug.WriteLine("[Velopack] Applying update and restarting RouteXia...");
                _velopack.ApplyUpdatesAndRestart(_pendingUpdate);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Velopack] Download & Install failed: {ex.Message}");
                return false;
            }
        }
    }
}

using System;
using System.IO;
using System.Text.Json;

namespace RouteXia.VpnClient.Auth
{
    /// <summary>
    /// Loads Firebase configuration from a firebase.config.json file located
    /// next to the executable. Values are NEVER hard-coded here.
    ///
    /// File location priority:
    ///   1. Same directory as RouteXia.exe (publish folder)
    ///   2. %LOCALAPPDATA%\RouteXia\firebase.config.json
    /// </summary>
    public sealed class FirebaseConfig
    {
        public string ApiKey    { get; init; } = string.Empty;
        public string ProjectId { get; init; } = string.Empty;
        public string AuthDomain { get; init; } = string.Empty;

        private static FirebaseConfig? _instance;

        public static FirebaseConfig Load()
        {
            if (_instance != null) return _instance;

            // Priority 1: beside the exe
            string exeDir    = AppContext.BaseDirectory;
            string exePath   = Path.Combine(exeDir, "firebase.config.json");

            // Priority 2: %LOCALAPPDATA%\RouteXia\
            string appData   = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RouteXia", "firebase.config.json");

            string? configPath = File.Exists(exePath)  ? exePath  :
                                 File.Exists(appData)  ? appData  : null;

            if (configPath == null)
                throw new FileNotFoundException(
                    "firebase.config.json not found. " +
                    "Copy firebase.config.example.json → firebase.config.json and fill in your Firebase API key. " +
                    $"Expected at: {exePath}");

            string json = File.ReadAllText(configPath);

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var cfg  = JsonSerializer.Deserialize<FirebaseConfig>(json, opts)
                       ?? throw new InvalidOperationException("firebase.config.json is malformed.");

            if (string.IsNullOrWhiteSpace(cfg.ApiKey) || cfg.ApiKey.StartsWith("PASTE_"))
                throw new InvalidOperationException(
                    "firebase.config.json contains placeholder values. " +
                    "Please fill in your real Firebase Web API Key.");

            _instance = cfg;
            return _instance;
        }
    }
}

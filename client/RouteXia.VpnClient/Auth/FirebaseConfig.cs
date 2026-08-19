using System;
using System.IO;
using System.Text.Json;

namespace RouteXia.VpnClient.Auth
{
    /// <summary>
    /// Loads Firebase configuration from a firebase.config.json file located
    /// next to the executable, with built-in client fallback defaults.
    /// </summary>
    public sealed class FirebaseConfig
    {
        public string ApiKey    { get; init; } = "AIzaSyBJtxmLbeeKe-XIcsKRhDkoPBTkmXPcPcQ";
        public string ProjectId { get; init; } = "routexia-3585f";
        public string AuthDomain { get; init; } = "routexia-3585f.firebaseapp.com";

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

            string? configPath = File.Exists(exePath) ? exePath :
                                 File.Exists(appData) ? appData : null;

            if (configPath != null)
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var cfg = JsonSerializer.Deserialize<FirebaseConfig>(json, opts);
                    if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ApiKey) && !cfg.ApiKey.StartsWith("PASTE_"))
                    {
                        _instance = cfg;
                        return _instance;
                    }
                }
                catch { /* Fallback to default */ }
            }

            // Fallback default client config
            _instance = new FirebaseConfig();
            return _instance;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RouteXia.VpnClient.Security;

namespace RouteXia.VpnClient.Api
{
    public sealed class RouteXiaApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private string _baseUrl;
        private string? _authToken;

        public UserDto? CurrentUser { get; private set; }
        public SubscriptionDto? CurrentSubscription { get; private set; }
        public List<RelayServerDto> ActiveRelays { get; private set; } = new();

        public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken) && CurrentUser != null;
        public bool CanConnect => CurrentSubscription?.CanConnect ?? false;

        public event Action? AuthStateChanged;

        private static readonly string TokenFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RouteXia", "auth.token");

        public RouteXiaApiClient(string baseUrl = "http://3.1.31.201:8080")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            LoadSavedToken();
        }

        public void SetBaseUrl(string url)
        {
            _baseUrl = url.TrimEnd('/');
        }

        // ── Auth Methods ──────────────────────────────────────────────────────────

        public async Task<(bool success, string message)> RegisterAsync(string email, string password)
        {
            try
            {
                string hwid = HwidGenerator.GetHwid();
                var req = new RegisterRequest
                {
                    Email = email,
                    Password = password,
                    HWID = hwid
                };

                var content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
                var res = await _http.PostAsync($"{_baseUrl}/api/v1/auth/register", content);

                string resBody = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    return (false, string.IsNullOrWhiteSpace(resBody) ? "Registration failed" : resBody);
                }

                var authRes = JsonSerializer.Deserialize<AuthResponse>(resBody);
                if (authRes != null)
                {
                    HandleAuthSuccess(authRes);
                    return (true, authRes.Subscription?.Message ?? "Registration successful!");
                }

                return (false, "Invalid response from server");
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}");
            }
        }

        public async Task<(bool success, string message)> LoginAsync(string email, string password)
        {
            try
            {
                string hwid = HwidGenerator.GetHwid();
                var req = new LoginRequest
                {
                    Email = email,
                    Password = password,
                    HWID = hwid
                };

                var content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
                var res = await _http.PostAsync($"{_baseUrl}/api/v1/auth/login", content);

                string resBody = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    return (false, string.IsNullOrWhiteSpace(resBody) ? "Invalid email or password" : resBody);
                }

                var authRes = JsonSerializer.Deserialize<AuthResponse>(resBody);
                if (authRes != null)
                {
                    HandleAuthSuccess(authRes);
                    return (true, "Login successful!");
                }

                return (false, "Invalid response from server");
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}");
            }
        }

        public async Task RefreshProfileAsync()
        {
            if (string.IsNullOrEmpty(_authToken)) return;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/user/profile");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    string body = await res.Content.ReadAsStringAsync();
                    var authRes = JsonSerializer.Deserialize<AuthResponse>(body);
                    if (authRes != null)
                    {
                        CurrentUser = authRes.User;
                        CurrentSubscription = authRes.Subscription;
                        if (authRes.Relays.Count > 0)
                            ActiveRelays = authRes.Relays;

                        AuthStateChanged?.Invoke();
                    }
                }
                else if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Logout();
                }
            }
            catch { /* Best effort */ }
        }

        public async Task<List<RelayServerDto>> FetchActiveRelaysAsync()
        {
            try
            {
                var res = await _http.GetAsync($"{_baseUrl}/api/v1/relays");
                if (res.IsSuccessStatusCode)
                {
                    string body = await res.Content.ReadAsStringAsync();
                    var list = JsonSerializer.Deserialize<List<RelayServerDto>>(body);
                    if (list != null && list.Count > 0)
                    {
                        ActiveRelays = list;
                        return list;
                    }
                }
            }
            catch { /* Fallback to default list */ }

            // Default fallback if backend is unreachable
            if (ActiveRelays.Count == 0)
            {
                ActiveRelays = new List<RelayServerDto>
                {
                    new RelayServerDto
                    {
                        ID = "default-sg",
                        RegionCode = "SG",
                        DisplayName = "Singapore 01 (AWS)",
                        Host = "3.1.31.201",
                        Port = 9001,
                        Priority = 1,
                        IsActive = true
                    }
                };
            }

            return ActiveRelays;
        }

        public void Logout()
        {
            _authToken = null;
            CurrentUser = null;
            CurrentSubscription = null;
            try
            {
                if (File.Exists(TokenFilePath))
                    File.Delete(TokenFilePath);
            }
            catch { }

            AuthStateChanged?.Invoke();
        }

        // ── Token Storage Helpers ─────────────────────────────────────────────────

        private void HandleAuthSuccess(AuthResponse res)
        {
            _authToken = res.Token;
            CurrentUser = res.User;
            CurrentSubscription = res.Subscription;
            if (res.Relays.Count > 0)
                ActiveRelays = res.Relays;

            SaveToken(_authToken);
            AuthStateChanged?.Invoke();
        }

        private void SaveToken(string token)
        {
            try
            {
                string dir = Path.GetDirectoryName(TokenFilePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(TokenFilePath, token);
            }
            catch { }
        }

        private void LoadSavedToken()
        {
            try
            {
                if (File.Exists(TokenFilePath))
                {
                    string token = File.ReadAllText(TokenFilePath).Trim();
                    if (!string.IsNullOrEmpty(token))
                    {
                        _authToken = token;
                        _ = RefreshProfileAsync();
                    }
                }
            }
            catch { }
        }

        public void Dispose() => _http.Dispose();
    }
}

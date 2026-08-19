using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
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

        public bool HasSavedToken => !string.IsNullOrEmpty(_authToken);
        public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken);
        public bool CanConnect => CurrentSubscription?.CanConnect ?? false;
        public bool CanManualSelectRelay => CurrentUser?.CanManualSelectRelay == true;

        public event Action? AuthStateChanged;
        public event Action<string>? UserBannedOrSuspended;

        private readonly System.Threading.Timer _profileSyncTimer;

        private static readonly string TokenFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RouteXia", "auth.token");

        private static readonly string SessionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RouteXia", "auth.session");

        public RouteXiaApiClient(string baseUrl = "https://api.routexia.in")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            LoadSavedToken();
            _profileSyncTimer = new System.Threading.Timer(async _ => await PeriodicProfileSyncAsync(), null, 5000, 10000);
        }

        private async Task PeriodicProfileSyncAsync()
        {
            if (!string.IsNullOrEmpty(_authToken))
            {
                await RefreshProfileAsync();
            }
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
                    try
                    {
                        using var doc = JsonDocument.Parse(resBody);
                        if (doc.RootElement.TryGetProperty("message", out var msgElem))
                        {
                            return (false, msgElem.GetString() ?? "Registration failed");
                        }
                    }
                    catch { }
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
                return (false, $"Server connection error: {ex.Message}");
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
                    try
                    {
                        using var doc = JsonDocument.Parse(resBody);
                        if (doc.RootElement.TryGetProperty("message", out var msgElem))
                        {
                            return (false, msgElem.GetString() ?? "Invalid email or password");
                        }
                    }
                    catch { }
                    return (false, string.IsNullOrWhiteSpace(resBody) ? "Invalid email or password" : resBody);
                }

                var authRes = JsonSerializer.Deserialize<AuthResponse>(resBody);
                if (authRes != null && !string.IsNullOrEmpty(authRes.Token))
                {
                    HandleAuthSuccess(authRes);
                    return (true, "Login successful!");
                }

                return (false, "Invalid response from server");
            }
            catch (Exception ex)
            {
                return (false, $"Server connection failed: {ex.Message}");
            }
        }

        public async Task<(bool success, string message)> AuthenticateWithFirebaseTokenAsync(string idToken, string? email)
        {
            try
            {
                string hwid = HwidGenerator.GetHwid();
                var req = new
                {
                    id_token = idToken,
                    email = email ?? string.Empty,
                    hwid = hwid
                };

                var content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
                var res = await _http.PostAsync($"{_baseUrl}/api/v1/auth/firebase", content);

                string resBody = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(resBody);
                        if (doc.RootElement.TryGetProperty("message", out var msgElem))
                        {
                            return (false, msgElem.GetString() ?? "Authentication failed");
                        }
                    }
                    catch { }
                    return (false, string.IsNullOrWhiteSpace(resBody) ? "Firebase authentication failed" : resBody);
                }

                var authRes = JsonSerializer.Deserialize<AuthResponse>(resBody);
                if (authRes != null)
                {
                    HandleAuthSuccess(authRes);
                    return (true, authRes.Subscription?.Message ?? "Authenticated successfully!");
                }

                return (false, "Invalid response from server");
            }
            catch (Exception ex)
            {
                return (false, $"Server connection error: {ex.Message}");
            }
        }

        public async Task RefreshProfileAsync()
        {
            if (string.IsNullOrEmpty(_authToken)) return;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/auth/profile");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

                var res = await _http.SendAsync(req);
                string resBody = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    var authRes = JsonSerializer.Deserialize<AuthResponse>(resBody);
                    if (authRes != null)
                    {
                        if (authRes.User?.IsBanned == true)
                        {
                            string banReason = authRes.User.BanReason ?? "Account suspended by Administrator";
                            Logout();
                            UserBannedOrSuspended?.Invoke(banReason);
                            return;
                        }

                        CurrentUser = authRes.User;
                        CurrentSubscription = authRes.Subscription;
                        if (authRes.Relays.Count > 0)
                            ActiveRelays = authRes.Relays;

                        SaveSessionData();
                        AuthStateChanged?.Invoke();
                    }
                }
                else if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized || res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    string errorMsg = ExtractErrorMessage(resBody, "Account suspended or session expired");
                    Logout();
                    if (errorMsg.Contains("suspended") || errorMsg.Contains("banned") || errorMsg.Contains("deleted"))
                    {
                        UserBannedOrSuspended?.Invoke(errorMsg);
                    }
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
                if (File.Exists(SessionFilePath))
                    File.Delete(SessionFilePath);
            }
            catch { }

            AuthStateChanged?.Invoke();
        }

        // ── Token Storage Helpers ─────────────────────────────────────────────────

        private class CachedSessionData
        {
            public string? Token { get; set; }
            public UserDto? User { get; set; }
            public SubscriptionDto? Subscription { get; set; }
            public List<RelayServerDto> Relays { get; set; } = new();
        }

        private void HandleAuthSuccess(AuthResponse res)
        {
            _authToken = res.Token;
            CurrentUser = res.User;
            CurrentSubscription = res.Subscription;
            if (res.Relays.Count > 0)
                ActiveRelays = res.Relays;

            SaveToken(_authToken);
            SaveSessionData();
            AuthStateChanged?.Invoke();
        }

        private static readonly byte[] TokenEntropy = Encoding.UTF8.GetBytes("RouteXia.Token.Entropy.v2026");

        private void SaveSessionData()
        {
            try
            {
                if (string.IsNullOrEmpty(_authToken)) return;
                var session = new CachedSessionData
                {
                    Token = _authToken,
                    User = CurrentUser,
                    Subscription = CurrentSubscription,
                    Relays = ActiveRelays
                };
                string json = JsonSerializer.Serialize(session);
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    TokenEntropy,
                    DataProtectionScope.CurrentUser);

                string dir = Path.GetDirectoryName(SessionFilePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(SessionFilePath, encryptedBytes);
            }
            catch { }
        }

        private void SaveToken(string token)
        {
            try
            {
                string dir = Path.GetDirectoryName(TokenFilePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                byte[] plainBytes = Encoding.UTF8.GetBytes(token);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    TokenEntropy,
                    DataProtectionScope.CurrentUser);

                File.WriteAllBytes(TokenFilePath, encryptedBytes);
            }
            catch { }
        }

        private void LoadSavedToken()
        {
            try
            {
                // 1. Try loading cached full session first (instant synchronous profile load)
                if (File.Exists(SessionFilePath))
                {
                    byte[] fileBytes = File.ReadAllBytes(SessionFilePath);
                    if (fileBytes.Length > 0)
                    {
                        try
                        {
                            byte[] decryptedBytes = ProtectedData.Unprotect(
                                fileBytes,
                                TokenEntropy,
                                DataProtectionScope.CurrentUser);
                            string json = Encoding.UTF8.GetString(decryptedBytes);
                            var session = JsonSerializer.Deserialize<CachedSessionData>(json);
                            if (session != null && !string.IsNullOrEmpty(session.Token))
                            {
                                _authToken = session.Token;
                                if (session.User != null) CurrentUser = session.User;
                                if (session.Subscription != null) CurrentSubscription = session.Subscription;
                                if (session.Relays?.Count > 0) ActiveRelays = session.Relays;

                                _ = RefreshProfileAsync();
                                return;
                            }
                        }
                        catch { }
                    }
                }

                // 2. Fallback to auth.token
                if (File.Exists(TokenFilePath))
                {
                    byte[] fileBytes = File.ReadAllBytes(TokenFilePath);
                    if (fileBytes.Length == 0) return;

                    string? token = null;
                    try
                    {
                        byte[] decryptedBytes = ProtectedData.Unprotect(
                            fileBytes,
                            TokenEntropy,
                            DataProtectionScope.CurrentUser);
                        token = Encoding.UTF8.GetString(decryptedBytes).Trim();
                    }
                    catch
                    {
                        // Fallback/migration for legacy plaintext token file
                        string legacyText = Encoding.UTF8.GetString(fileBytes).Trim();
                        if (legacyText.StartsWith("eyJ"))
                        {
                            token = legacyText;
                            // Automatically upgrade to DPAPI encrypted format
                            SaveToken(token);
                        }
                    }

                    if (!string.IsNullOrEmpty(token))
                    {
                        _authToken = token;
                        _ = RefreshProfileAsync();
                    }
                }
            }
            catch { }
        }

        // ── Session Management (Live Session Reporting) ──────────────────────────────

        public async Task<(bool success, string message, SessionConnectResponse? data)> ReportSessionConnectAsync(SessionConnectRequest request)
        {
            if (string.IsNullOrEmpty(_authToken)) return (false, "Not authenticated", null);

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/sessions/connect");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                req.Content = content;

                var res = await _http.SendAsync(req);
                string resBody = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    string errorMsg = ExtractErrorMessage(resBody, "Session connect rejected by server");
                    return (false, errorMsg, null);
                }

                var data = JsonSerializer.Deserialize<SessionConnectResponse>(resBody);
                return (true, "Session connected", data);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public async Task<(bool success, string message)> ReportSessionHeartbeatAsync(SessionHeartbeatRequest request)
        {
            if (string.IsNullOrEmpty(_authToken)) return (false, "Not authenticated");

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/sessions/heartbeat");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                req.Content = content;

                var res = await _http.SendAsync(req);
                string resBody = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    return (false, ExtractErrorMessage(resBody, "Heartbeat failed"));
                }

                return (true, "OK");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool success, string message)> ReportSessionDisconnectAsync(SessionDisconnectRequest request)
        {
            if (string.IsNullOrEmpty(_authToken)) return (false, "Not authenticated");

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/sessions/disconnect");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                req.Content = content;

                var res = await _http.SendAsync(req);
                string resBody = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    return (false, ExtractErrorMessage(resBody, "Disconnect failed"));
                }

                return (true, "OK");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static string ExtractErrorMessage(string resBody, string fallback)
        {
            if (string.IsNullOrWhiteSpace(resBody)) return fallback;
            try
            {
                using var doc = JsonDocument.Parse(resBody);
                if (doc.RootElement.TryGetProperty("message", out var msgElem))
                {
                    if (msgElem.ValueKind == JsonValueKind.String)
                        return msgElem.GetString() ?? fallback;
                    if (msgElem.ValueKind == JsonValueKind.Array && msgElem.GetArrayLength() > 0)
                        return msgElem[0].GetString() ?? fallback;
                }
                if (doc.RootElement.TryGetProperty("error", out var errElem) && errElem.ValueKind == JsonValueKind.String)
                {
                    return errElem.GetString() ?? fallback;
                }
            }
            catch { }
            return resBody;
        }

        public void Dispose() => _http.Dispose();
    }
}

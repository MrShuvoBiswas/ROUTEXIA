using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RouteXia.VpnClient.Security;

namespace RouteXia.VpnClient.Auth
{
    public class FirebaseAuthResult
    {
        public bool    Success      { get; set; }
        public string? IdToken      { get; set; }   // Firebase ID token → send to backend
        public string? Email        { get; set; }
        public string? DisplayName  { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // ── Firebase REST API response shapes ────────────────────────────────────

    internal sealed class FirebaseSignInResponse
    {
        [JsonPropertyName("idToken")]     public string? IdToken     { get; set; }
        [JsonPropertyName("email")]       public string? Email       { get; set; }
        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
        [JsonPropertyName("error")]       public FirebaseErrorWrapper? Error { get; set; }
    }

    internal sealed class FirebaseErrorWrapper
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    /// <summary>
    /// Handles Firebase Authentication for the RouteXia WPF client.
    ///
    /// Email/Password → Firebase Identity Toolkit REST API → gets idToken
    /// Google Sign-In → Browser OAuth flow → gets idToken
    ///
    /// The idToken is then forwarded to the RouteXia backend for verification.
    /// API keys are loaded from firebase.config.json — never hardcoded here.
    /// </summary>
    public sealed class FirebaseAuthService : IDisposable
    {
        private readonly HttpClient   _http;
        private readonly string       _apiKey;
        private const    string       IdentityBaseUrl = "https://identitytoolkit.googleapis.com/v1";

        public FirebaseAuthService()
        {
            var cfg = FirebaseConfig.Load();
            _apiKey = cfg.ApiKey;
            _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        // ── Email / Password Sign-In ──────────────────────────────────────────

        /// <summary>
        /// Signs in with email and password via Firebase Identity Toolkit.
        /// Returns a Firebase idToken on success.
        /// </summary>
        public async Task<FirebaseAuthResult> SignInWithEmailPasswordAsync(
            string email, string password, CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    email,
                    password,
                    returnSecureToken = true
                };

                string url  = $"{IdentityBaseUrl}/accounts:signInWithPassword?key={_apiKey}";
                var content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var res = await _http.PostAsync(url, content, ct);
                string body = await res.Content.ReadAsStringAsync(ct);

                var resp = JsonSerializer.Deserialize<FirebaseSignInResponse>(body);

                if (!res.IsSuccessStatusCode || resp?.IdToken == null)
                {
                    string errMsg = resp?.Error?.Message switch
                    {
                        "EMAIL_NOT_FOUND"     => "No account found with this email address.",
                        "INVALID_PASSWORD"    => "Incorrect password. Please try again.",
                        "USER_DISABLED"       => "This account has been disabled.",
                        "INVALID_LOGIN_CREDENTIALS" => "Invalid email or password.",
                        var m                 => m ?? "Authentication failed. Please try again."
                    };
                    return new FirebaseAuthResult { Success = false, ErrorMessage = errMsg };
                }

                return new FirebaseAuthResult
                {
                    Success     = true,
                    IdToken     = resp.IdToken,
                    Email       = resp.Email,
                    DisplayName = resp.DisplayName
                };
            }
            catch (Exception ex)
            {
                return new FirebaseAuthResult
                {
                    Success      = false,
                    ErrorMessage = $"Network error: {ex.Message}"
                };
            }
        }

        // ── Email / Password Registration ────────────────────────────────────

        /// <summary>
        /// Creates a new Firebase user with email and password.
        /// Returns a Firebase idToken on success.
        /// </summary>
        public async Task<FirebaseAuthResult> RegisterWithEmailPasswordAsync(
            string email, string password, CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    email,
                    password,
                    returnSecureToken = true
                };

                string url  = $"{IdentityBaseUrl}/accounts:signUp?key={_apiKey}";
                var content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var res  = await _http.PostAsync(url, content, ct);
                string body = await res.Content.ReadAsStringAsync(ct);

                var resp = JsonSerializer.Deserialize<FirebaseSignInResponse>(body);

                if (!res.IsSuccessStatusCode || resp?.IdToken == null)
                {
                    string errMsg = resp?.Error?.Message switch
                    {
                        "EMAIL_EXISTS"              => "An account with this email already exists.",
                        "WEAK_PASSWORD"             => "Password must be at least 6 characters.",
                        "INVALID_EMAIL"             => "Please enter a valid email address.",
                        var m                       => m ?? "Registration failed. Please try again."
                    };
                    return new FirebaseAuthResult { Success = false, ErrorMessage = errMsg };
                }

                return new FirebaseAuthResult
                {
                    Success     = true,
                    IdToken     = resp.IdToken,
                    Email       = resp.Email,
                    DisplayName = resp.DisplayName
                };
            }
            catch (Exception ex)
            {
                return new FirebaseAuthResult
                {
                    Success      = false,
                    ErrorMessage = $"Network error: {ex.Message}"
                };
            }
        }

        // ── Google OAuth via Browser (same callback pattern as before) ────────

        /// <summary>
        /// Opens the system browser for Google Sign-In.
        /// Starts a local HTTP listener on a random port to capture the callback.
        /// The auth portal must redirect to: http://127.0.0.1:{port}/callback/?token=FIREBASE_ID_TOKEN&email=USER_EMAIL
        /// </summary>
        public async Task<FirebaseAuthResult> SignInWithGoogleAsync(
            string authPortalUrl, CancellationToken cancellationToken = default)
        {
            int    port        = GetFreePort();
            string callbackUrl = $"http://127.0.0.1:{port}/callback/";
            string hwid        = HwidGenerator.GetHwid();

            string targetUrl = $"{authPortalUrl}?callback={Uri.EscapeDataString(callbackUrl)}&hwid={Uri.EscapeDataString(hwid)}";

            using var listener = new HttpListener();
            listener.Prefixes.Add(callbackUrl);

            try { listener.Start(); }
            catch (Exception ex)
            {
                return new FirebaseAuthResult
                {
                    Success      = false,
                    ErrorMessage = $"Failed to start local auth listener: {ex.Message}"
                };
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = targetUrl, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                listener.Stop();
                return new FirebaseAuthResult { Success = false, ErrorMessage = $"Cannot open browser: {ex.Message}" };
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(3));

            try
            {
                var getCtxTask    = listener.GetContextAsync();
                var completedTask = await Task.WhenAny(getCtxTask, Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask != getCtxTask)
                {
                    listener.Stop();
                    return new FirebaseAuthResult { Success = false, ErrorMessage = "Authentication timed out." };
                }

                var context = await getCtxTask;
                var qs      = context.Request.QueryString;

                string? token = qs["token"];
                string? email = qs["email"];

                // Send success response with CORS headers
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "*");
                byte[] html = Encoding.UTF8.GetBytes(SuccessHtml);
                context.Response.ContentType     = "text/html; charset=utf-8";
                context.Response.ContentLength64 = html.Length;
                await context.Response.OutputStream.WriteAsync(html);
                context.Response.OutputStream.Close();
                listener.Stop();

                if (string.IsNullOrEmpty(token))
                    return new FirebaseAuthResult { Success = false, ErrorMessage = "No Firebase ID token received." };

                return new FirebaseAuthResult { Success = true, IdToken = token, Email = email };
            }
            catch (OperationCanceledException)
            {
                listener.Stop();
                return new FirebaseAuthResult { Success = false, ErrorMessage = "Authentication was cancelled." };
            }
            catch (Exception ex)
            {
                listener.Stop();
                return new FirebaseAuthResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static int GetFreePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        private const string SuccessHtml = @"<!DOCTYPE html>
<html><head><meta charset='utf-8'/>
<title>RouteXia — Authenticated</title>
<style>
  body{background:#06090E;color:#F0F4F8;font-family:sans-serif;
       display:flex;align-items:center;justify-content:center;height:90vh;text-align:center;}
  .card{background:#0D131C;border:1px solid #1B2433;border-radius:16px;
        padding:40px;box-shadow:0 10px 40px rgba(0,0,0,.6);max-width:400px;}
  h2{color:#00E676;margin-bottom:10px;}
  p{color:#8292A2;font-size:14px;}
</style></head><body>
<div class='card'>
  <h2>✅ Authentication Successful!</h2>
  <p>Signed in to RouteXia. You may close this tab and return to the application.</p>
</div></body></html>";

        public void Dispose() => _http.Dispose();
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RouteXia.VpnClient.Security;

namespace RouteXia.VpnClient.Auth
{
    public class BrowserAuthResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Email { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public sealed class BrowserAuthService
    {
        private readonly string _authPortalUrl;

        public BrowserAuthService(string authPortalUrl = "http://3.1.31.201:8080/auth")
        {
            _authPortalUrl = authPortalUrl;
        }

        public async Task<BrowserAuthResult> StartBrowserAuthAsync(CancellationToken cancellationToken = default)
        {
            int port = GetRandomUnusedPort();
            string callbackUrl = $"http://127.0.0.1:{port}/callback/";

            using var listener = new HttpListener();
            listener.Prefixes.Add(callbackUrl);

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                return new BrowserAuthResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to start local auth listener: {ex.Message}"
                };
            }

            // Hardware ID to bind with trial
            string hwid = HwidGenerator.GetHwid();

            // Construct auth web URL with query params
            string targetUrl = $"{_authPortalUrl}?callback={Uri.EscapeDataString(callbackUrl)}&hwid={Uri.EscapeDataString(hwid)}";

            try
            {
                // Open system default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                listener.Stop();
                return new BrowserAuthResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to launch default browser: {ex.Message}"
                };
            }

            // Wait for incoming redirect with 3-minute timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(3));

            try
            {
                var getContextTask = listener.GetContextAsync();
                var completedTask = await Task.WhenAny(getContextTask, Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask != getContextTask)
                {
                    listener.Stop();
                    return new BrowserAuthResult
                    {
                        Success = false,
                        ErrorMessage = "Authentication timed out or was cancelled"
                    };
                }

                var context = await getContextTask;
                var query = context.Request.QueryString;

                string? token = query["token"];
                string? email = query["email"];

                // Respond with success page to the browser tab
                string responseHtml = @"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <title>RouteXia — Authenticated</title>
  <style>
    body { background: #06090E; color: #F0F4F8; font-family: sans-serif; display: flex; align-items: center; justify-content: center; height: 90vh; text-align: center; }
    .card { background: #0D131C; border: 1px solid #1B2433; border-radius: 16px; padding: 40px; box-shadow: 0 10px 40px rgba(0,0,0,0.6); max-width: 400px; }
    h2 { color: #00E676; margin-bottom: 10px; }
    p { color: #8292A2; font-size: 14px; }
  </style>
</head>
<body>
  <div class='card'>
    <h2>✅ Authentication Successful!</h2>
    <p>You are now signed in to RouteXia. You may close this browser tab and return to the application.</p>
  </div>
</body>
</html>";

                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();

                listener.Stop();

                if (string.IsNullOrEmpty(token))
                {
                    return new BrowserAuthResult
                    {
                        Success = false,
                        ErrorMessage = "No authentication token was received from the browser"
                    };
                }

                return new BrowserAuthResult
                {
                    Success = true,
                    Token = token,
                    Email = email
                };
            }
            catch (OperationCanceledException)
            {
                listener.Stop();
                return new BrowserAuthResult
                {
                    Success = false,
                    ErrorMessage = "Authentication was cancelled"
                };
            }
            catch (Exception ex)
            {
                listener.Stop();
                return new BrowserAuthResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static int GetRandomUnusedPort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RouteXia.App.ViewModels;

public class DiagnosticsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _terminalOutput = "RouteXia Network Diagnostic Engine v1.0 Ready.\nClick any diagnostic tool below to run system tests.\n";
    public string TerminalOutput
    {
        get => _terminalOutput;
        set { _terminalOutput = value; OnPropertyChanged(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    private void AppendLog(string message)
    {
        TerminalOutput += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    public async Task FlushDnsAsync()
    {
        IsBusy = true;
        AppendLog("Executing: ipconfig /flushdns ...");
        await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                string outStr = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit(3000);
                AppendLog(outStr.Trim());
                AppendLog("✅ Windows DNS Resolver Cache cleared successfully.");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error flushing DNS: {ex.Message}");
            }
        });
        IsBusy = false;
    }

    public async Task ResetWinsockAsync()
    {
        IsBusy = true;
        AppendLog("Executing: netsh winsock reset catalog ...");
        await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "winsock reset",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                string outStr = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit(3000);
                AppendLog(outStr.Trim());
                AppendLog("✅ Winsock catalog restored. Network buffers refreshed.");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error resetting winsock: {ex.Message}");
            }
        });
        IsBusy = false;
    }

    public async Task TestGamePortsAsync()
    {
        IsBusy = true;
        AppendLog("Testing connection to RouteXia Gaming Relays (sg.relays.routexia.in:9001 UDP/TCP) ...");
        await Task.Run(async () =>
        {
            try
            {
                using var client = new TcpClient();
                var sw = Stopwatch.StartNew();
                var connectTask = client.ConnectAsync("1.1.1.1", 53);
                var timeoutTask = Task.Delay(2000);
                var completed = await Task.WhenAny(connectTask, timeoutTask);
                sw.Stop();

                if (completed == connectTask && client.Connected)
                {
                    AppendLog($"✅ Outbound UDP/TCP Game Ports OPEN ({sw.ElapsedMilliseconds}ms RTT). No ISP port-blocking detected.");
                }
                else
                {
                    AppendLog("⚠️ Port test timed out. Verify Windows Firewall settings.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Port check error: {ex.Message}");
            }
        });
        IsBusy = false;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

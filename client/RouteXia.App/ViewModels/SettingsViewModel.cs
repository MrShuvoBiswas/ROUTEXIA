using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Win32;

namespace RouteXia.App.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private bool _autoConnectOnGameLaunch = true;
    private bool _startWithWindows = false;
    private bool _minimizeToTray = true;

    public bool AutoConnectOnGameLaunch
    {
        get => _autoConnectOnGameLaunch;
        set { _autoConnectOnGameLaunch = value; OnPropertyChanged(); }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            _startWithWindows = value;
            OnPropertyChanged();
            SetStartupRegistry(value);
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set { _minimizeToTray = value; OnPropertyChanged(); }
    }

    private static void SetStartupRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

            if (enable && !string.IsNullOrEmpty(exePath))
            {
                key.SetValue("RouteXia", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("RouteXia", false);
            }
        }
        catch { /* Permission check gracefully handled */ }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

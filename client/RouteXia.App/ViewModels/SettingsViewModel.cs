using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using RouteXia.App.Services;

namespace RouteXia.App.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

public class UserSettings
{
    public bool AutoConnectOnGameLaunch { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
}

public class SettingsViewModel : INotifyPropertyChanged
{
    private static readonly string SettingsFilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RouteXia",
        "settings.json");

    private bool _autoConnectOnGameLaunch = true;
    private bool _startWithWindows = false;
    private bool _minimizeToTray = true;

    public bool AutoConnectOnGameLaunch
    {
        get => _autoConnectOnGameLaunch;
        set
        {
            if (_autoConnectOnGameLaunch != value)
            {
                _autoConnectOnGameLaunch = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows != value)
            {
                _startWithWindows = value;
                OnPropertyChanged();
                SetStartupRegistry(value);
                SaveSettings();
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (_minimizeToTray != value)
            {
                _minimizeToTray = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public SettingsViewModel()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (System.IO.File.Exists(SettingsFilePath))
            {
                var json = System.IO.File.ReadAllText(SettingsFilePath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(json);
                if (settings != null)
                {
                    _autoConnectOnGameLaunch = settings.AutoConnectOnGameLaunch;
                    _minimizeToTray = settings.MinimizeToTray;
                    _startWithWindows = settings.StartWithWindows;
                }
            }
        }
        catch { /* Fallback gracefully to defaults */ }
    }

    public void SaveSettings()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            var settings = new UserSettings
            {
                AutoConnectOnGameLaunch = _autoConnectOnGameLaunch,
                MinimizeToTray = _minimizeToTray,
                StartWithWindows = _startWithWindows
            };

            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(SettingsFilePath, json);
        }
        catch { /* best effort */ }
    }

    // ── Application Updates (T008, T009) ─────────────────────────────────────
    private readonly UpdateManager _updateManager = UpdateManager.Instance;
    private bool _isCheckingForUpdate;
    private bool _isUpdateAvailable;
    private bool _isDownloadingUpdate;
    private double _downloadProgress;
    private string _updateStatusText = "Up to date";
    private string _latestVersionText = "v0.1.0-beta";
    private string _downloadUrl = string.Empty;

    public string AppVersionText => $"v{UpdateManager.GetCurrentVersion()}-beta";

    public bool IsCheckingForUpdate
    {
        get => _isCheckingForUpdate;
        set { _isCheckingForUpdate = value; OnPropertyChanged(); }
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set { _isUpdateAvailable = value; OnPropertyChanged(); }
    }

    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        set { _isDownloadingUpdate = value; OnPropertyChanged(); }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(); }
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        set { _updateStatusText = value; OnPropertyChanged(); }
    }

    public string LatestVersionText
    {
        get => _latestVersionText;
        set { _latestVersionText = value; OnPropertyChanged(); }
    }

    private ICommand? _checkForUpdatesCommand;
    public ICommand CheckForUpdatesCommand =>
        _checkForUpdatesCommand ??= new RelayCommand(async _ => await CheckForUpdatesAsync());

    private ICommand? _installUpdateCommand;
    public ICommand InstallUpdateCommand =>
        _installUpdateCommand ??= new RelayCommand(async _ => await InstallUpdateAsync());

    public async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdate) return;
        IsCheckingForUpdate = true;
        UpdateStatusText = "Checking for updates...";

        try
        {
            var result = await _updateManager.CheckForUpdateAsync();
            if (result.IsUpdateAvailable)
            {
                IsUpdateAvailable = true;
                LatestVersionText = $"v{result.LatestVersion}";
                _downloadUrl = result.DownloadUrl;
                UpdateStatusText = $"New version {LatestVersionText} available!";
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateStatusText = "You are on the latest version.";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"Update check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingForUpdate = false;
        }
    }

    public async Task InstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_downloadUrl) || IsDownloadingUpdate) return;

        IsDownloadingUpdate = true;
        var progress = new Progress<double>(p => DownloadProgress = p);

        bool success = await _updateManager.DownloadAndInstallUpdateAsync(_downloadUrl, progress);
        if (!success)
        {
            UpdateStatusText = "Download failed. Please try again later.";
            IsDownloadingUpdate = false;
        }
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

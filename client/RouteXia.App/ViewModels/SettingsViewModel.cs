using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;

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

public class RelayRegionPreference : INotifyPropertyChanged
{
    public string RegionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FlagEmoji { get; set; } = string.Empty;

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isPrimaryPreferred;
    public bool IsPrimaryPreferred
    {
        get => _isPrimaryPreferred;
        set
        {
            if (_isPrimaryPreferred != value)
            {
                _isPrimaryPreferred = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

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

    // ── Relay Region Preferences (T010, T011, T040) ──────────────────────────
    public ObservableCollection<RelayRegionPreference> RelayRegions { get; } = [];

    public bool CanSaveRelayPreferences => RelayRegions.Any(r => r.IsEnabled);
    public bool HasRegionValidationError => !CanSaveRelayPreferences;

    private ICommand? _saveRelayPreferencesCommand;
    public ICommand SaveRelayPreferencesCommand =>
        _saveRelayPreferencesCommand ??= new RelayCommand(_ => SaveRelayPreferences(), _ => CanSaveRelayPreferences);

    private string _saveStatusMessage = string.Empty;
    public string SaveStatusMessage
    {
        get => _saveStatusMessage;
        set { _saveStatusMessage = value; OnPropertyChanged(); }
    }

    public SettingsViewModel()
    {
        LoadRelayPreferences();
    }

    public void SaveRelayPreferences()
    {
        if (!CanSaveRelayPreferences) return;

        try
        {
            var enabledIds = RelayRegions.Where(r => r.IsEnabled).Select(r => r.RegionId).ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(enabledIds);
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RouteXia", "relay_preferences.json");
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            System.IO.File.WriteAllText(path, json);
            SaveStatusMessage = "Preferences saved successfully";
        }
        catch (Exception ex)
        {
            SaveStatusMessage = $"Save error: {ex.Message}";
        }
    }

    private void LoadRelayPreferences()
    {
        RelayRegions.Clear();
        var defaults = new List<RelayRegionPreference>
        {
            new() { RegionId = "SG", DisplayName = "Singapore (SG)", FlagEmoji = "🇸🇬", IsEnabled = true, IsPrimaryPreferred = true },
            new() { RegionId = "IN", DisplayName = "India (IN)", FlagEmoji = "🇮🇳", IsEnabled = true, IsPrimaryPreferred = false },
            new() { RegionId = "AE", DisplayName = "Dubai (AE)", FlagEmoji = "🇦🇪", IsEnabled = true, IsPrimaryPreferred = false }
        };

        try
        {
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RouteXia", "relay_preferences.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var enabledIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (enabledIds != null && enabledIds.Count > 0)
                {
                    foreach (var item in defaults)
                    {
                        item.IsEnabled = enabledIds.Contains(item.RegionId);
                    }
                }
            }
        }
        catch { /* Fallback gracefully to defaults */ }

        foreach (var item in defaults)
        {
            item.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CanSaveRelayPreferences));
                OnPropertyChanged(nameof(HasRegionValidationError));
                (SaveRelayPreferencesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };
            RelayRegions.Add(item);
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

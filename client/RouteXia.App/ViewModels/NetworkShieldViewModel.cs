using System.ComponentModel;
using System.Runtime.CompilerServices;
using RouteXia.VpnClient.KillSwitch;
using RouteXia.VpnClient.Interception;

namespace RouteXia.App.ViewModels;

public class NetworkShieldViewModel : INotifyPropertyChanged
{
    private readonly KillSwitchManager _killSwitch;
    private readonly WinDivertInterceptor _interceptor;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsKillSwitchEnabled
    {
        get => _killSwitch.IsActive;
        set
        {
            if (value)
                _killSwitch.Activate();
            else
                _killSwitch.Deactivate();

            OnPropertyChanged();
            OnPropertyChanged(nameof(KillSwitchStatusText));
        }
    }

    public string KillSwitchStatusText => IsKillSwitchEnabled ? "ARMED & BLOCKING" : "MONITORING (AUTO-FAILOVER)";

    public bool IsWinDivertActive => _interceptor.IsRunning;
    public string DriverStatusText => IsWinDivertActive ? "Kernel Filter Active" : "Standby (Ready)";

    public long TotalPacketsCaptured => _interceptor.PacketsCaptured;

    public NetworkShieldViewModel(KillSwitchManager killSwitch, WinDivertInterceptor interceptor)
    {
        _killSwitch = killSwitch;
        _interceptor = interceptor;

        _killSwitch.KillSwitchActivated += (_, _) =>
        {
            OnPropertyChanged(nameof(IsKillSwitchEnabled));
            OnPropertyChanged(nameof(KillSwitchStatusText));
        };

        _killSwitch.KillSwitchDeactivated += (_, _) =>
        {
            OnPropertyChanged(nameof(IsKillSwitchEnabled));
            OnPropertyChanged(nameof(KillSwitchStatusText));
        };
    }

    public void ToggleKillSwitch()
    {
        IsKillSwitchEnabled = !IsKillSwitchEnabled;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

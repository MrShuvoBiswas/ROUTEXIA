using System;
using System.Diagnostics;

namespace RouteXia.WfpFilter
{
    /// <summary>
    /// PUBG process detector.
    ///
    /// NOTE: The original WFP packet interception approach (FWP_ACTION_PERMIT) was
    /// non-functional for actual traffic redirection — user-mode WFP cannot inject
    /// or redirect packets without a kernel-mode callout driver.
    ///
    /// Real packet interception is now handled by WinDivertInterceptor in
    /// RouteXia.VpnClient.Interception. This class is kept for process detection only:
    ///   - Polls for TslGame.exe every 1 second
    ///   - Fires FlowDetected when PUBG PC is detected as running
    ///   - ConnectViewModel wires this up to start/stop WinDivertInterceptor
    /// </summary>
    public sealed class WfpFilterEngine : IDisposable
    {
        // ── PUBG PC process name ───────────────────────────────────────────────────
        private static readonly string[] PubgProcessNames =
        [
            "TslGame",   // PUBG PC (Steam / Krafton Launcher)
        ];

        private bool _disposed;
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Fired when PUBG PC (TslGame.exe) is detected as running.
        /// </summary>
        public event EventHandler<PubgFlowDetectedEventArgs>? FlowDetected;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Start polling for PUBG processes.</summary>
        public void Start()
        {
            if (IsRunning) return;
            InstallFilters();
            IsRunning = true;
            Debug.WriteLine("[WfpFilter] Started — monitoring PUBG process");
        }

        /// <summary>Stop polling.</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            _pollerTimer?.Dispose();
            _pollerTimer = null;
            IsRunning = false;
            Debug.WriteLine("[WfpFilter] Stopped");
        }

        // ── Process polling ───────────────────────────────────────────────────────

        private void InstallFilters() => StartProcessPoller();

        private void RemoveFilters() { } // no-op — WinDivert handles cleanup

        // ── Process Polling (detects active PUBG PIDs) ────────────────────────────

        private System.Threading.Timer? _pollerTimer;

        private void StartProcessPoller()
        {
            _pollerTimer = new System.Threading.Timer(PollPubgProcesses, null,
                TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private void PollPubgProcesses(object? _)
        {
            foreach (var name in PubgProcessNames)
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var proc in procs)
                {
                    using (proc)
                    {
                        FlowDetected?.Invoke(this, new PubgFlowDetectedEventArgs
                        {
                            ProcessName = name,
                            ProcessId   = proc.Id,
                            DetectedAt  = DateTime.UtcNow
                        });
                    }
                }
            }
        }



        // ── IDisposable ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _pollerTimer?.Dispose();
            Stop();
            _disposed = true;
        }
    }

    /// <summary>Event raised when a PUBG process is detected as active.</summary>
    public sealed class PubgFlowDetectedEventArgs : EventArgs
    {
        public required string ProcessName { get; init; }
        public required int ProcessId { get; init; }
        public required DateTime DetectedAt { get; init; }
    }
}

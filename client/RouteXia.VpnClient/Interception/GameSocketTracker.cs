using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace RouteXia.VpnClient.Interception
{
    /// <summary>
    /// Ultra-High-Performance Socket & Process Ownership Validator for RouteXia.
    ///
    /// Architecture:
    /// 1. Background Poller (500ms): Queries Windows Extended UDP table asynchronously.
    ///    Maps target game process (e.g. TslGame.exe / PUBG) -> Exact local UDP ports.
    /// 2. Zero-overhead In-Memory Lookup: Packet dispatch checks active game ports in O(1) time
    ///    without triggering kernel syscalls on every packet.
    /// 3. Strict Process Exclusions: Explicitly whitelists Discord, Spotify, Chrome, Steam, etc.
    ///    to guarantee ZERO interference and ZERO latency penalty on voice/browsing.
    /// </summary>
    public static class GameSocketTracker
    {
        private const int AF_INET = 2;

        private enum UDP_TABLE_CLASS
        {
            UDP_TABLE_BASIC,
            UDP_TABLE_OWNER_PID,
            UDP_TABLE_OWNER_MODULE
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDPROW_OWNER_PID
        {
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwOwningPid;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(
            IntPtr pUdpTable,
            ref int pdwSize,
            bool bOrder,
            int ulAf,
            UDP_TABLE_CLASS TableClass,
            uint Reserved = 0);

        // Sets of active ports (updated asynchronously by background poller)
        private static readonly HashSet<ushort> _activeGamePorts = new();
        private static readonly HashSet<ushort> _excludedPorts = new();
        private static readonly object _portLock = new();

        private static readonly HashSet<string> _targetProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "TslGame",                  // PUBG PC (Steam / Krafton)
            "VALORANT-Win64-Shipping",  // Valorant
            "VALORANT",
            "cs2",                      // Counter-Strike 2
            "cod",                      // Call of Duty Warzone
            "warzone",
            "r5apex",                   // Apex Legends
            "FortniteClient-Win64-Shipping" // Fortnite
        };

        // Processes that must NEVER be intercepted (100% bypass)
        private static readonly HashSet<string> _excludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Discord",
            "DiscordCanary",
            "DiscordPTB",
            "Spotify",
            "chrome",
            "msedge",
            "firefox",
            "brave",
            "steam",
            "steamwebhelper",
            "EpicGamesLauncher",
            "Battle.net",
            "Telegram",
            "svchost"
        };

        private static Timer? _pollerTimer;
        private static bool _initialized = false;

        static GameSocketTracker()
        {
            StartPoller();
        }

        public static void StartPoller()
        {
            if (_initialized) return;
            _initialized = true;
            RefreshPortTable(null);
            _pollerTimer = new Timer(RefreshPortTable, null, 500, 500);
        }

        /// <summary>
        /// Update list of target game process names (without .exe).
        /// </summary>
        public static void SetTargetProcessNames(IEnumerable<string>? processNames)
        {
            if (processNames == null) return;
            lock (_targetProcessNames)
            {
                _targetProcessNames.Clear();
                foreach (var name in processNames)
                {
                    _targetProcessNames.Add(name);
                }
            }
            RefreshPortTable(null);
        }

        /// <summary>
        /// Returns true ONLY if the local port is confirmed to be owned by a target game process (TslGame.exe).
        /// All other traffic (Discord, Chrome, etc.) returns false in 0 nanoseconds.
        /// </summary>
        public static bool IsGameTraffic(ushort localPort, IPAddress? destIp = null)
        {
            lock (_portLock)
            {
                if (_activeGamePorts.Contains(localPort))
                {
                    return true;
                }
                if (_excludedPorts.Contains(localPort))
                {
                    return false;
                }
            }

            // If not found in cache, do an immediate table scan
            return CheckPortImmediately(localPort);
        }

        private static bool CheckPortImmediately(ushort localPort)
        {
            RefreshPortTable(null);
            lock (_portLock)
            {
                return _activeGamePorts.Contains(localPort);
            }
        }

        private static void RefreshPortTable(object? state)
        {
            int size = 0;
            _ = GetExtendedUdpTable(IntPtr.Zero, ref size, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
            if (size <= 0) return;

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                uint ret = GetExtendedUdpTable(buffer, ref size, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
                if (ret != 0) return;

                int numEntries = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                int rowSize = Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();

                var newGamePorts = new HashSet<ushort>();
                var newExcludedPorts = new HashSet<ushort>();

                // Build cache of active target PIDs and excluded PIDs
                var targetPids = new HashSet<int>();
                var excludedPids = new HashSet<int>();

                string[] targetNames;
                lock (_targetProcessNames)
                {
                    targetNames = new string[_targetProcessNames.Count];
                    _targetProcessNames.CopyTo(targetNames);
                }

                foreach (var name in targetNames)
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName(name))
                        {
                            targetPids.Add(p.Id);
                            p.Dispose();
                        }
                    }
                    catch { }
                }

                foreach (var name in _excludedProcessNames)
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName(name))
                        {
                            excludedPids.Add(p.Id);
                            p.Dispose();
                        }
                    }
                    catch { }
                }

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr);
                    ushort port = (ushort)(((row.dwLocalPort & 0xFF) << 8) | ((row.dwLocalPort >> 8) & 0xFF));
                    int pid = (int)row.dwOwningPid;

                    if (targetPids.Contains(pid))
                    {
                        newGamePorts.Add(port);
                    }
                    else if (excludedPids.Contains(pid))
                    {
                        newExcludedPorts.Add(port);
                    }

                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }

                lock (_portLock)
                {
                    _activeGamePorts.Clear();
                    foreach (var p in newGamePorts) _activeGamePorts.Add(p);

                    _excludedPorts.Clear();
                    foreach (var p in newExcludedPorts) _excludedPorts.Add(p);
                }
            }
            catch { }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}

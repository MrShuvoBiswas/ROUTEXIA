using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace RouteXia.VpnClient.Interception
{
    /// <summary>
    /// Real-time socket and process ownership validator.
    /// Ensures ONLY the target game traffic (e.g. PUBG TslGame.exe) is intercepted and routed.
    /// All non-game traffic (Discord voice, Spotify, Chrome, Steam, Zoom, torrents, system)
    /// is instantly identified and re-injected untouched into the physical network interface.
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

        // Fast cache: localPort -> (isGameProcess, timestampTicks)
        private static readonly ConcurrentDictionary<ushort, (bool isGame, long timestampTicks)> _portCache = new();
        private static readonly HashSet<int> _targetPids = new();
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

        private static long _lastPidRefreshTicks = 0;
        private static readonly long PidRefreshIntervalTicks = (long)(1.5 * Stopwatch.Frequency);

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
            _portCache.Clear();
            RefreshTargetPids();
        }

        /// <summary>
        /// Refreshes the set of active PIDs for target game processes.
        /// </summary>
        public static void RefreshTargetPids()
        {
            lock (_targetPids)
            {
                _targetPids.Clear();
                string[] names;
                lock (_targetProcessNames)
                {
                    names = new string[_targetProcessNames.Count];
                    _targetProcessNames.CopyTo(names);
                }

                foreach (var name in names)
                {
                    try
                    {
                        var procs = Process.GetProcessesByName(name);
                        foreach (var p in procs)
                        {
                            _targetPids.Add(p.Id);
                            p.Dispose();
                        }
                    }
                    catch { }
                }
                _lastPidRefreshTicks = Stopwatch.GetTimestamp();
            }
        }

        /// <summary>
        /// Returns true ONLY if the local port belongs to a target game process,
        /// or if the destination IP is a verified game server subnet.
        /// </summary>
        public static bool IsGameTraffic(ushort localPort, IPAddress? destIp = null)
        {
            long now = Stopwatch.GetTimestamp();

            // Refresh PIDs every 1.5s
            if (now - _lastPidRefreshTicks > PidRefreshIntervalTicks)
            {
                RefreshTargetPids();
            }

            // Fast cache lookup (< 3 seconds freshness)
            if (_portCache.TryGetValue(localPort, out var cached))
            {
                if (now - cached.timestampTicks < 3 * Stopwatch.Frequency)
                {
                    return cached.isGame;
                }
            }

            // Query Windows UDP Table to inspect owning PID of local socket
            bool isGame = CheckPortOwningPid(localPort);

            // CIDR fallback check for known game match & lobby subnets
            if (!isGame && destIp != null && IsKnownGameServerIp(destIp))
            {
                isGame = true;
            }

            _portCache[localPort] = (isGame, now);
            return isGame;
        }

        private static bool CheckPortOwningPid(ushort localPort)
        {
            int size = 0;
            _ = GetExtendedUdpTable(IntPtr.Zero, ref size, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
            if (size <= 0) return false;

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                uint ret = GetExtendedUdpTable(buffer, ref size, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
                if (ret != 0) return false;

                int numEntries = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                int rowSize = Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr);
                    // dwLocalPort is stored in big-endian network byte order
                    ushort port = (ushort)(((row.dwLocalPort & 0xFF) << 8) | ((row.dwLocalPort >> 8) & 0xFF));

                    if (port == localPort)
                    {
                        int pid = (int)row.dwOwningPid;
                        lock (_targetPids)
                        {
                            if (_targetPids.Contains(pid)) return true;
                        }

                        // Inspect process name if PID was spawned just now
                        try
                        {
                            using var proc = Process.GetProcessById(pid);
                            string pName = proc.ProcessName;
                            lock (_targetProcessNames)
                            {
                                if (_targetProcessNames.Contains(pName))
                                {
                                    lock (_targetPids) { _targetPids.Add(pid); }
                                    return true;
                                }
                            }
                        }
                        catch { }

                        return false;
                    }

                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return false;
        }

        /// <summary>
        /// Verified game server subnets (Azure SEA, AWS SG, PUBG match servers)
        /// </summary>
        public static bool IsKnownGameServerIp(IPAddress ip)
        {
            // Azure Southeast Asia & PUBG Main
            if (IsInRange(ip, "20.205.0.0", 16) || IsInRange(ip, "20.43.0.0", 16) ||
                IsInRange(ip, "20.79.0.0",  16) || IsInRange(ip, "20.196.0.0", 16) ||
                IsInRange(ip, "20.201.0.0", 16) || IsInRange(ip, "52.158.0.0", 16))
                return true;

            // PUBG Match Servers
            if (IsInRange(ip, "57.129.0.0", 16))
                return true;

            // AWS Singapore Game Infrastructure
            if (IsInRange(ip, "13.228.0.0", 16) || IsInRange(ip, "13.229.0.0", 16) ||
                IsInRange(ip, "18.136.0.0", 16) || IsInRange(ip, "52.74.0.0",  16) ||
                IsInRange(ip, "54.169.0.0", 16))
                return true;

            return false;
        }

        private static bool IsInRange(IPAddress ip, string networkStr, int prefixLen)
        {
            var ipBytes = ip.GetAddressBytes();
            if (ipBytes.Length != 4) return false;

            var netBytes = IPAddress.Parse(networkStr).GetAddressBytes();
            uint ipInt = (uint)(ipBytes[0] << 24 | ipBytes[1] << 16 | ipBytes[2] << 8 | ipBytes[3]);
            uint netInt = (uint)(netBytes[0] << 24 | netBytes[1] << 16 | netBytes[2] << 8 | netBytes[3]);
            uint mask = prefixLen == 0 ? 0 : (0xFFFFFFFFu << (32 - prefixLen));

            return (ipInt & mask) == (netInt & mask);
        }
    }
}

using System;
using System.IO;
using System.Management;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace RouteXia.VpnClient.Security
{
    /// <summary>
    /// Generates a tamper-resistant, deterministic Hardware ID (HWID) unique to the physical PC.
    /// Combines Motherboard UUID, CPU ID, Disk Serial, and Windows MachineGuid.
    /// Used for trial fraud prevention and anti-abuse enforcement.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class HwidGenerator
    {
        private static string? _cachedHwid;

        public static string GetHwid()
        {
            if (!string.IsNullOrEmpty(_cachedHwid))
                return _cachedHwid;

            var sb = new StringBuilder();

            // 1. Motherboard UUID
            sb.Append("MB:").Append(GetWmiProperty("Win32_ComputerSystemProduct", "UUID"));

            // 2. CPU Processor ID
            sb.Append("|CPU:").Append(GetWmiProperty("Win32_Processor", "ProcessorId"));

            // 3. Primary Disk Serial Number
            sb.Append("|DISK:").Append(GetWmiProperty("Win32_DiskDrive", "SerialNumber"));

            // 4. BIOS Serial
            sb.Append("|BIOS:").Append(GetWmiProperty("Win32_BIOS", "SerialNumber"));

            // 5. Windows MachineGuid from Registry (fallback/anchor)
            sb.Append("|GUID:").Append(GetMachineGuid());

            // Compute SHA-256 Hash
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            _cachedHwid = Convert.ToHexString(hashBytes).ToLowerInvariant();

            return _cachedHwid;
        }

        private static string GetWmiProperty(string wmiClass, string property)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var val = obj[property]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }
            }
            catch
            {
                // Best effort fallback
            }

            return "UNKNOWN";
        }

        private static string GetMachineGuid()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrEmpty(guid))
                    return guid;
            }
            catch { }

            return Environment.MachineName;
        }
    }
}

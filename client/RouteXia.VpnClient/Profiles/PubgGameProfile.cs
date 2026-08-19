using System;
using System.Collections.Generic;
using System.Net;

namespace RouteXia.VpnClient.Profiles
{
    /// <summary>
    /// Dedicated isolation and routing profile for PUBG: Battlegrounds (PC).
    /// Restricts kernel capture strictly to PUBG game ports (7000-8999, 10000-20000, 5222).
    /// Guarantees 0% interference with Discord voice (UDP 50000+) and other applications.
    /// </summary>
    public sealed class PubgGameProfile : BaseGameProfile
    {
        public override string GameId => "pubg";
        public override string DisplayName => "PUBG: Battlegrounds";
        public override string ShortName => "PUBG";

        public override IReadOnlyList<string> ProcessNames { get; } = new[]
        {
            "TslGame",
            "TslGame_UC",
            "TslGame_BE",
            "ExecPubg"
        };

        // WinDivert filter: Only capture PUBG's exact UDP game ports (7000-8999, 10000-20000, 5222).
        // High UDP ports (50000-65535) used by Discord are completely excluded in kernel!
        public override string WinDivertFilter =>
            "outbound and udp and " +
            "ip.DstAddr != 127.0.0.1 and " +
            "((udp.DstPort >= 7000 and udp.DstPort <= 8999) or " +
            "(udp.DstPort >= 10000 and udp.DstPort <= 20000) or " +
            "udp.DstPort == 5222)";

        public override IReadOnlyList<string> CidrRanges { get; } = new[]
        {
            // Microsoft Azure Southeast Asia (Main PUBG Game Servers)
            "20.205.0.0/16",
            "20.43.0.0/16",
            "20.79.0.0/16",
            "20.196.0.0/16",
            "20.201.0.0/16",
            "52.158.0.0/16",

            // PUBG Dedicated Match Server clusters
            "57.129.0.0/16",

            // AWS Singapore (Lobby, Matchmaking, QoS)
            "13.228.0.0/16",
            "13.229.0.0/16",
            "18.136.0.0/16",
            "52.74.0.0/16",
            "54.169.0.0/16",
            "54.254.0.0/16",
            "54.255.0.0/16",

            // Tencent Cloud SEA / G-Core
            "150.109.0.0/16",
            "129.226.0.0/16"
        };

        public override bool IsGamePort(ushort destPort)
        {
            return (destPort >= 7000 && destPort <= 8999) ||
                   (destPort >= 10000 && destPort <= 20000) ||
                   destPort == 5222;
        }

        public override string FormatServerDisplay(IPAddress destIp, ushort destPort)
        {
            string ipStr = destIp.ToString();

            if (IsInRange(destIp, "20.205.0.0", 16) || IsInRange(destIp, "20.43.0.0", 16) ||
                IsInRange(destIp, "20.79.0.0", 16) || IsInRange(destIp, "20.196.0.0", 16) ||
                IsInRange(destIp, "20.201.0.0", 16) || IsInRange(destIp, "52.158.0.0", 16))
            {
                return $"Azure SEA — {ipStr}:{destPort}";
            }

            if (IsInRange(destIp, "57.129.0.0", 16))
            {
                return $"PUBG Match Server — {ipStr}:{destPort}";
            }

            if (IsInRange(destIp, "13.228.0.0", 16) || IsInRange(destIp, "13.229.0.0", 16) ||
                IsInRange(destIp, "18.136.0.0", 16) || IsInRange(destIp, "52.74.0.0", 16) ||
                IsInRange(destIp, "54.169.0.0", 16))
            {
                return $"AWS Singapore — {ipStr}:{destPort}";
            }

            return $"PUBG Match Server — {ipStr}:{destPort}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;

namespace RouteXia.VpnClient.Profiles
{
    /// <summary>
    /// Dedicated isolation and routing profile for Apex Legends (EA / Respawn).
    /// Restricts kernel capture strictly to Apex Legends game server ports (1024-1124, 18000-18100, 27000-27050, 37005-37020).
    /// </summary>
    public sealed class ApexGameProfile : BaseGameProfile
    {
        public override string GameId => "apex";
        public override string DisplayName => "Apex Legends";
        public override string ShortName => "Apex";

        public override IReadOnlyList<string> ProcessNames { get; } = new[]
        {
            "r5apex",
            "r5apex_dx12"
        };

        public override string WinDivertFilter =>
            "outbound and udp and " +
            "ip.DstAddr != 127.0.0.1 and " +
            "((udp.DstPort >= 1024 and udp.DstPort <= 1124) or " +
            "(udp.DstPort >= 18000 and udp.DstPort <= 18100) or " +
            "(udp.DstPort >= 27000 and udp.DstPort <= 27050) or " +
            "(udp.DstPort >= 37005 and udp.DstPort <= 37020))";

        public override IReadOnlyList<string> CidrRanges { get; } = new[]
        {
            // EA Multiplay / GCP / AWS Singapore
            "108.128.0.0/16",
            "18.136.0.0/16",
            "35.240.0.0/16",
            "34.87.0.0/16",
            "52.0.0.0/8",
            "54.0.0.0/8"
        };

        public override bool IsGamePort(ushort destPort)
        {
            return (destPort >= 1024 && destPort <= 1124) ||
                   (destPort >= 18000 && destPort <= 18100) ||
                   (destPort >= 27000 && destPort <= 27050) ||
                   (destPort >= 37005 && destPort <= 37020);
        }

        public override string FormatServerDisplay(IPAddress destIp, ushort destPort)
        {
            string ipStr = destIp.ToString();

            if (IsInRange(destIp, "35.240.0.0", 16) || IsInRange(destIp, "18.136.0.0", 16))
            {
                return $"EA Singapore Multiplay — {ipStr}:{destPort}";
            }

            return $"Apex Match Server — {ipStr}:{destPort}";
        }
    }
}

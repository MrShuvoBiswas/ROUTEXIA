using System;
using System.Collections.Generic;
using System.Net;

namespace RouteXia.VpnClient.Profiles
{
    /// <summary>
    /// Dedicated isolation and routing profile for Valorant (Riot Games).
    /// Restricts kernel capture strictly to Riot Direct game server ports (7000-8000, 20000-24000).
    /// </summary>
    public sealed class ValorantGameProfile : BaseGameProfile
    {
        public override string GameId => "valorant";
        public override string DisplayName => "Valorant";
        public override string ShortName => "Valorant";

        public override IReadOnlyList<string> ProcessNames { get; } = new[]
        {
            "VALORANT-Win64-Shipping",
            "VALORANT",
            "RiotClientServices"
        };

        public override string WinDivertFilter =>
            "outbound and udp and " +
            "ip.DstAddr != 127.0.0.1 and " +
            "((udp.DstPort >= 7000 and udp.DstPort <= 8000) or " +
            "(udp.DstPort >= 20000 and udp.DstPort <= 24000))";

        public override IReadOnlyList<string> CidrRanges { get; } = new[]
        {
            // Riot Direct Global Routing & AWS Singapore
            "99.83.0.0/16",
            "75.2.0.0/16",
            "192.207.0.0/16",
            "151.106.0.0/16",
            "162.249.72.0/22",
            "162.249.76.0/22",
            "162.249.79.0/24"
        };

        public override bool IsGamePort(ushort destPort)
        {
            return (destPort >= 7000 && destPort <= 8000) ||
                   (destPort >= 20000 && destPort <= 24000);
        }

        public override string FormatServerDisplay(IPAddress destIp, ushort destPort)
        {
            string ipStr = destIp.ToString();

            if (IsInRange(destIp, "151.106.0.0", 16) || IsInRange(destIp, "162.249.72.0", 22))
            {
                return $"Riot Direct SEA — {ipStr}:{destPort}";
            }

            return $"Valorant Match Server — {ipStr}:{destPort}";
        }
    }
}

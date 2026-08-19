using System;
using System.Collections.Generic;
using System.Net;

namespace RouteXia.VpnClient.Profiles
{
    /// <summary>
    /// Dedicated isolation and routing profile for Counter-Strike 2 (CS2).
    /// Restricts kernel capture strictly to Valve SDR and Steam game server ports (27000-27050, 3478, 4379-4380).
    /// </summary>
    public sealed class Cs2GameProfile : BaseGameProfile
    {
        public override string GameId => "cs2";
        public override string DisplayName => "Counter-Strike 2";
        public override string ShortName => "CS2";

        public override IReadOnlyList<string> ProcessNames { get; } = new[]
        {
            "cs2"
        };

        public override string WinDivertFilter =>
            "outbound and udp and " +
            "ip.DstAddr != 127.0.0.1 and " +
            "((udp.DstPort >= 27000 and udp.DstPort <= 27050) or " +
            "udp.DstPort == 3478 or " +
            "(udp.DstPort >= 4379 and udp.DstPort <= 4380))";

        public override IReadOnlyList<string> CidrRanges { get; } = new[]
        {
            // Valve Steam Datagram Relay (SDR) Global Networks
            "155.133.0.0/16",
            "162.254.192.0/18",
            "162.254.196.0/22",
            "162.254.198.0/23",
            "146.66.152.0/21",
            "185.25.180.0/22",
            "208.78.164.0/22",
            "45.121.184.0/22"
        };

        public override bool IsGamePort(ushort destPort)
        {
            return (destPort >= 27000 && destPort <= 27050) ||
                   destPort == 3478 ||
                   (destPort >= 4379 && destPort <= 4380);
        }

        public override string FormatServerDisplay(IPAddress destIp, ushort destPort)
        {
            string ipStr = destIp.ToString();

            if (IsInRange(destIp, "155.133.0.0", 16) || IsInRange(destIp, "162.254.192.0", 18))
            {
                return $"Valve SDR Singapore — {ipStr}:{destPort}";
            }

            return $"CS2 Match Server — {ipStr}:{destPort}";
        }
    }
}

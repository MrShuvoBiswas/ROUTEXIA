using System;
using System.Collections.Generic;
using System.Net;

namespace RouteXia.VpnClient.Profiles
{
    /// <summary>
    /// Dedicated isolation and routing profile for Call of Duty: Warzone (Activision / Demonware).
    /// Restricts kernel capture strictly to Warzone game server ports (3074-3076, 27014-27050, 3478-3480).
    /// </summary>
    public sealed class WarzoneGameProfile : BaseGameProfile
    {
        public override string GameId => "warzone";
        public override string DisplayName => "Call of Duty: Warzone";
        public override string ShortName => "Warzone";

        public override IReadOnlyList<string> ProcessNames { get; } = new[]
        {
            "cod",
            "warzone",
            "ModernWarfare",
            "bootstrapper"
        };

        public override string WinDivertFilter =>
            "outbound and udp and " +
            "ip.DstAddr != 127.0.0.1 and " +
            "(udp.DstPort == 3074 or " +
            "udp.DstPort == 3075 or " +
            "udp.DstPort == 3076 or " +
            "(udp.DstPort >= 27014 and udp.DstPort <= 27050) or " +
            "(udp.DstPort >= 3478 and udp.DstPort <= 3480))";

        public override IReadOnlyList<string> CidrRanges { get; } = new[]
        {
            // Demonware Activision Server Clusters
            "185.34.104.0/22",
            "185.34.106.0/23",
            "104.0.0.0/8",
            "12.0.0.0/8"
        };

        public override bool IsGamePort(ushort destPort)
        {
            return destPort == 3074 ||
                   destPort == 3075 ||
                   destPort == 3076 ||
                   (destPort >= 27014 && destPort <= 27050) ||
                   (destPort >= 3478 && destPort <= 3480);
        }

        public override string FormatServerDisplay(IPAddress destIp, ushort destPort)
        {
            return $"Demonware SEA — {destIp}:{destPort}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;

namespace RouteXia.VpnClient.Profiles
{
    /// <summary>
    /// Dedicated isolation and routing profile for Fortnite (Epic Games).
    /// Restricts kernel capture strictly to Fortnite game server ports (9000-9100, 7000-8000, 15000-15100).
    /// </summary>
    public sealed class FortniteGameProfile : BaseGameProfile
    {
        public override string GameId => "fortnite";
        public override string DisplayName => "Fortnite";
        public override string ShortName => "Fortnite";

        public override IReadOnlyList<string> ProcessNames { get; } = new[]
        {
            "FortniteClient-Win64-Shipping",
            "FortniteClient-Win64-Shipping_BE",
            "FortniteClient-Win64-Shipping_EAC",
            "FortniteLauncher"
        };

        public override string WinDivertFilter =>
            "outbound and udp and " +
            "ip.DstAddr != 127.0.0.1 and " +
            "((udp.DstPort >= 9000 and udp.DstPort <= 9100) or " +
            "(udp.DstPort >= 7000 and udp.DstPort <= 8000) or " +
            "(udp.DstPort >= 15000 and udp.DstPort <= 15100))";

        public override IReadOnlyList<string> CidrRanges { get; } = new[]
        {
            // Epic Games AWS Fleet / QoS
            "52.0.0.0/8",
            "54.0.0.0/8",
            "18.0.0.0/8",
            "3.0.0.0/8",
            "13.0.0.0/8"
        };

        public override bool IsGamePort(ushort destPort)
        {
            return (destPort >= 9000 && destPort <= 9100) ||
                   (destPort >= 7000 && destPort <= 8000) ||
                   (destPort >= 15000 && destPort <= 15100);
        }

        public override string FormatServerDisplay(IPAddress destIp, ushort destPort)
        {
            return $"Fortnite AWS SEA — {destIp}:{destPort}";
        }
    }
}

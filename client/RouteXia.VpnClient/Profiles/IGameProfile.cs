using System.Collections.Generic;
using System.Net;

namespace RouteXia.VpnClient.Profiles
{
    /// <summary>
    /// Contract for game-specific routing, packet interception, port filtering, and CIDR subnet isolation.
    /// Every supported game has its own dedicated profile implementation.
    /// </summary>
    public interface IGameProfile
    {
        /// <summary>Unique game identifier (e.g. "pubg", "cs2", "valorant").</summary>
        string GameId { get; }

        /// <summary>Full display name (e.g. "PUBG: Battlegrounds").</summary>
        string DisplayName { get; }

        /// <summary>Short display name (e.g. "PUBG").</summary>
        string ShortName { get; }

        /// <summary>Executable process names (without .exe) used to track game sockets.</summary>
        IReadOnlyList<string> ProcessNames { get; }

        /// <summary>
        /// Precision WinDivert kernel filter that restricts packet capture strictly to this game's ports.
        /// Non-game applications (Discord voice, browsers, torrents) are excluded at the driver level.
        /// </summary>
        string WinDivertFilter { get; }

        /// <summary>List of known server CIDR network blocks (e.g. "20.205.0.0/16").</summary>
        IReadOnlyList<string> CidrRanges { get; }

        /// <summary>Checks if a destination UDP port belongs to this game.</summary>
        bool IsGamePort(ushort destPort);

        /// <summary>Fast binary check if a destination IP falls within the game's official server CIDRs.</summary>
        bool MatchesCidr(IPAddress destIp);

        /// <summary>
        /// Validates whether an intercepted packet strictly belongs to this game.
        /// </summary>
        bool ValidatePacket(ushort srcPort, IPAddress destIp, ushort destPort);

        /// <summary>
        /// Formats friendly display text for discovered match servers (e.g. "Azure SEA — 20.205.177.143:7000").
        /// </summary>
        string FormatServerDisplay(IPAddress destIp, ushort destPort);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace RouteXia.VpnClient.Profiles
{
    /// <summary>
    /// Central registry of dedicated game profiles for ROUTEXIA.
    /// Provides exact game isolation profiles by ID or fallback default.
    /// </summary>
    public static class GameProfileRegistry
    {
        private static readonly Dictionary<string, IGameProfile> _profiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["pubg"]     = new PubgGameProfile(),
            ["cs2"]      = new Cs2GameProfile(),
            ["valorant"] = new ValorantGameProfile(),
            ["apex"]     = new ApexGameProfile(),
            ["fortnite"] = new FortniteGameProfile(),
            ["warzone"]  = new WarzoneGameProfile()
        };

        public static IReadOnlyCollection<IGameProfile> AllProfiles => _profiles.Values;

        /// <summary>
        /// Retrieves the dedicated game profile by game ID.
        /// Falls back to PUBG profile if not found.
        /// </summary>
        public static IGameProfile GetProfile(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return _profiles["pubg"];
            }

            if (_profiles.TryGetValue(gameId.Trim(), out var profile))
            {
                return profile;
            }

            return _profiles["pubg"];
        }

        /// <summary>
        /// Attempts to find a profile matching a process name.
        /// </summary>
        public static IGameProfile? FindProfileByProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return null;

            string cleanName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName.Substring(0, processName.Length - 4)
                : processName;

            foreach (var profile in _profiles.Values)
            {
                if (profile.ProcessNames.Any(p => p.Equals(cleanName, StringComparison.OrdinalIgnoreCase)))
                {
                    return profile;
                }
            }

            return null;
        }
    }
}

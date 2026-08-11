namespace RouteXia.App.Data;

/// <summary>
/// Central registry of games and applications with official image assets.
/// </summary>
public static class GameRegistry
{
    /// <summary>All registered games with official icons (supported + coming soon).</summary>
    public static IReadOnlyList<GameDefinition> AllGames { get; } = new List<GameDefinition>
    {
        // ═══════════════════════════════════════════════════════
        // ── 1. FULLY SUPPORTED GAME (ACTIVE) ─────────────────
        // ═══════════════════════════════════════════════════════

        new GameDefinition
        {
            Id            = "pubg",
            Name          = "PUBG: Battlegrounds",
            ShortName     = "PUBG",
            IconGlyph     = "Shield24",
            ImagePath     = "/Resources/Images/pubg_icon.png",
            ProcessNames  = ["TslGame"],
            CidrFile      = "pubg-cidr-ranges.json",
            Category      = "Games",
            LaunchUri     = "steam://rungameid/578080",
            FallbackExe   = "TslGame.exe",
            IsSupported   = true,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        // ═══════════════════════════════════════════════════════
        // ── 2. COMING SOON (WITH OFFICIAL IMAGE ASSETS) ───────
        // ═══════════════════════════════════════════════════════

        new GameDefinition
        {
            Id            = "valorant",
            Name          = "Valorant",
            ShortName     = "Valorant",
            IconGlyph     = "Target24",
            ImagePath     = "/Resources/Images/valorant_icon.png",
            ProcessNames  = ["VALORANT-Win64-Shipping", "VALORANT"],
            Category      = "Games",
            LaunchUri     = null,
            IsSupported   = false,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        new GameDefinition
        {
            Id            = "cs2",
            Name          = "Counter-Strike 2",
            ShortName     = "CS2",
            IconGlyph     = "Flash24",
            ImagePath     = "/Resources/Images/cs2_icon.png",
            ProcessNames  = ["cs2"],
            Category      = "Games",
            LaunchUri     = "steam://rungameid/730",
            IsSupported   = false,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        new GameDefinition
        {
            Id            = "warzone",
            Name          = "Call of Duty: Warzone",
            ShortName     = "Warzone",
            IconGlyph     = "Games24",
            ImagePath     = "/Resources/Images/warzone_icon.png",
            ProcessNames  = ["cod", "warzone"],
            Category      = "Games",
            LaunchUri     = null,
            IsSupported   = false,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        new GameDefinition
        {
            Id            = "apex",
            Name          = "Apex Legends",
            ShortName     = "Apex",
            IconGlyph     = "Trophy24",
            ImagePath     = "/Resources/Images/apex_icon.png",
            ProcessNames  = ["r5apex"],
            Category      = "Games",
            LaunchUri     = "steam://rungameid/1172470",
            IsSupported   = false,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        new GameDefinition
        {
            Id            = "fortnite",
            Name          = "Fortnite",
            ShortName     = "Fortnite",
            IconGlyph     = "Games24",
            ImagePath     = "/Resources/Images/fortnite_icon.png",
            ProcessNames  = ["FortniteClient-Win64-Shipping"],
            Category      = "Games",
            LaunchUri     = null,
            IsSupported   = false,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        new GameDefinition
        {
            Id            = "discord",
            Name          = "Discord",
            ShortName     = "Discord",
            IconGlyph     = "Chat24",
            ImagePath     = "/Resources/Images/discord_icon.png",
            ProcessNames  = ["Discord"],
            Category      = "Voice",
            LaunchUri     = null,
            IsSupported   = false,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },
    };

    /// <summary>Only games that are fully supported with routing rules.</summary>
    public static IReadOnlyList<GameDefinition> SupportedGames =>
        AllGames.Where(g => g.IsSupported).ToList();

    /// <summary>Get game by ID, or null if not found.</summary>
    public static GameDefinition? GetById(string id) =>
        AllGames.FirstOrDefault(g => g.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>All unique categories across all games.</summary>
    public static IReadOnlyList<string> AllCategories =>
        new[] { "All" }.Concat(AllGames.Select(g => g.Category).Distinct().OrderBy(c => c)).ToList();
}

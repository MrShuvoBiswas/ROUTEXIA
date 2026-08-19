namespace RouteXia.App.Data;

/// <summary>
/// Central registry of games and applications with official image assets and dedicated isolation profiles.
/// </summary>
public static class GameRegistry
{
    /// <summary>All registered games with official icons and dedicated routing profiles.</summary>
    public static IReadOnlyList<GameDefinition> AllGames { get; } = new List<GameDefinition>
    {
        // ═══════════════════════════════════════════════════════
        // ── 1. PUBG: BATTLEGROUNDS ────────────────────────────
        // ═══════════════════════════════════════════════════════
        new GameDefinition
        {
            Id            = "pubg",
            Name          = "PUBG: Battlegrounds",
            ShortName     = "PUBG",
            IconGlyph     = "Shield24",
            ImagePath     = "pack://application:,,,/RouteXia;component/Resources/Images/pubg_icon.png",
            ProcessNames  = ["TslGame", "TslGame_UC", "TslGame_BE", "ExecPubg"],
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
        // ── 2. COUNTER-STRIKE 2 ───────────────────────────────
        // ═══════════════════════════════════════════════════════
        new GameDefinition
        {
            Id            = "cs2",
            Name          = "Counter-Strike 2",
            ShortName     = "CS2",
            IconGlyph     = "Flash24",
            ImagePath     = "pack://application:,,,/RouteXia;component/Resources/Images/cs2_icon.png",
            ProcessNames  = ["cs2"],
            Category      = "Games",
            LaunchUri     = "steam://rungameid/730",
            FallbackExe   = "cs2.exe",
            IsSupported   = true,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        // ═══════════════════════════════════════════════════════
        // ── 3. VALORANT ───────────────────────────────────────
        // ═══════════════════════════════════════════════════════
        new GameDefinition
        {
            Id            = "valorant",
            Name          = "Valorant",
            ShortName     = "Valorant",
            IconGlyph     = "Target24",
            ImagePath     = "pack://application:,,,/RouteXia;component/Resources/Images/valorant_icon.png",
            ProcessNames  = ["VALORANT-Win64-Shipping", "VALORANT", "RiotClientServices"],
            Category      = "Games",
            LaunchUri     = null,
            FallbackExe   = "VALORANT.exe",
            IsSupported   = true,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        // ═══════════════════════════════════════════════════════
        // ── 4. CALL OF DUTY: WARZONE ──────────────────────────
        // ═══════════════════════════════════════════════════════
        new GameDefinition
        {
            Id            = "warzone",
            Name          = "Call of Duty: Warzone",
            ShortName     = "Warzone",
            IconGlyph     = "Games24",
            ImagePath     = "pack://application:,,,/RouteXia;component/Resources/Images/warzone_icon.png",
            ProcessNames  = ["cod", "warzone", "ModernWarfare", "bootstrapper"],
            Category      = "Games",
            LaunchUri     = null,
            FallbackExe   = "cod.exe",
            IsSupported   = true,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        // ═══════════════════════════════════════════════════════
        // ── 5. APEX LEGENDS ───────────────────────────────────
        // ═══════════════════════════════════════════════════════
        new GameDefinition
        {
            Id            = "apex",
            Name          = "Apex Legends",
            ShortName     = "Apex",
            IconGlyph     = "Trophy24",
            ImagePath     = "pack://application:,,,/RouteXia;component/Resources/Images/apex_icon.png",
            ProcessNames  = ["r5apex", "r5apex_dx12"],
            Category      = "Games",
            LaunchUri     = "steam://rungameid/1172470",
            FallbackExe   = "r5apex.exe",
            IsSupported   = true,
            Region        = "SEA",
            RegionName    = "South East Asia",
            RegionBadge   = "SG"
        },

        // ═══════════════════════════════════════════════════════
        // ── 6. FORTNITE ───────────────────────────────────────
        // ═══════════════════════════════════════════════════════
        new GameDefinition
        {
            Id            = "fortnite",
            Name          = "Fortnite",
            ShortName     = "Fortnite",
            IconGlyph     = "Games24",
            ImagePath     = "pack://application:,,,/RouteXia;component/Resources/Images/fortnite_icon.png",
            ProcessNames  = ["FortniteClient-Win64-Shipping", "FortniteClient-Win64-Shipping_BE", "FortniteClient-Win64-Shipping_EAC", "FortniteLauncher"],
            Category      = "Games",
            LaunchUri     = null,
            FallbackExe   = "FortniteLauncher.exe",
            IsSupported   = true,
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

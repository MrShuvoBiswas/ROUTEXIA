namespace RouteXia.App.Data;

/// <summary>
/// Represents a game or application that RouteXia can optimize.
/// To add a new game, create a new GameDefinition and register it in GameRegistry.
/// </summary>
public class GameDefinition
{
    /// <summary>Unique identifier (e.g. "pubg", "valorant", "cs2").</summary>
    public required string Id { get; init; }

    /// <summary>Full display name (e.g. "PUBG: Battlegrounds").</summary>
    public required string Name { get; init; }

    /// <summary>Short name for compact UI (e.g. "PUBG").</summary>
    public required string ShortName { get; init; }

    /// <summary>WPF-UI SymbolRegular icon name for display.</summary>
    public required string IconGlyph { get; init; }

    /// <summary>Process names to detect when game is running (without .exe).</summary>
    public required string[] ProcessNames { get; init; }

    /// <summary>CIDR ranges JSON file for split-tunnel routing (relative to Data/).</summary>
    public string? CidrFile { get; init; }

    /// <summary>Category for library filtering.</summary>
    public required string Category { get; init; }

    /// <summary>Launch URI (e.g. "steam://rungameid/578080").</summary>
    public string? LaunchUri { get; init; }

    /// <summary>Fallback executable name if URI launch fails.</summary>
    public string? FallbackExe { get; init; }

    /// <summary>True if this game is fully supported with routing. False = "Coming Soon".</summary>
    public bool IsSupported { get; init; }

    /// <summary>Default server region code.</summary>
    public string Region { get; init; } = "SEA";

    /// <summary>Default server region display name.</summary>
    public string RegionName { get; init; } = "South East Asia";

    /// <summary>Two-letter region code for the server card badge.</summary>
    public string RegionBadge { get; init; } = "SG";

    /// <summary>Optional image icon path (e.g. "/Resources/Images/pubg_icon.png").</summary>
    public string? ImagePath { get; init; }

    /// <summary>True if a custom image path is provided.</summary>
    public bool HasImage => !string.IsNullOrEmpty(ImagePath);
}

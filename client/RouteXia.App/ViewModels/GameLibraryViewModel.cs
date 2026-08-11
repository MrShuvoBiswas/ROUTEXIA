using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RouteXia.App.Data;

namespace RouteXia.App.ViewModels;

/// <summary>
/// ViewModel for the Game Library page.
/// Manages the game grid, category filtering, and search.
/// </summary>
public class GameLibraryViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>All games to display in the grid (filtered).</summary>
    public ObservableCollection<GameDefinition> FilteredGames { get; } = [];

    /// <summary>Available categories for the sidebar filter.</summary>
    public ObservableCollection<string> Categories { get; } = [];

    private string _selectedCategory = "All";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            ApplyFilters();
        }
    }

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            OnPropertyChanged();
            ApplyFilters();
        }
    }

    private GameDefinition? _selectedGame;
    public GameDefinition? SelectedGame
    {
        get => _selectedGame;
        set
        {
            _selectedGame = value;
            OnPropertyChanged();
        }
    }

    public GameLibraryViewModel()
    {
        // Load categories
        foreach (var cat in GameRegistry.AllCategories)
            Categories.Add(cat);

        // Initial load — show all
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        FilteredGames.Clear();

        var query = SearchQuery?.Trim() ?? string.Empty;

        foreach (var game in GameRegistry.AllGames)
        {
            // Category filter
            if (SelectedCategory != "All" &&
                !game.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
                continue;

            // Search filter
            if (!string.IsNullOrEmpty(query) &&
                !game.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !game.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredGames.Add(game);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

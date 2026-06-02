using System.ComponentModel;

namespace IntelligentPersonalHealthOptimization.Models.Recipe;

/// <summary>
/// Read-only model mapped from MSSQL RecipeDB.Recipes table.
/// </summary>
public class RecipeItem : INotifyPropertyChanged
{
    public int RecipeID { get; set; }
    public int CategoryID { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string? PrepTime { get; set; }
    public string? CookTime { get; set; }
    public string? RestTime { get; set; }
    public string? Servings { get; set; }
    public string? Difficulty { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public byte[]? RecipeImage { get; set; }
    public DateTime DateAdded { get; set; }
    public DateTime? LastMadeOn { get; set; }
    public int? Rating { get; set; }
    public bool IsFavorite { get; set; }

    // Navigation / computed
    public string CategoryName { get; set; } = string.Empty;
    public int IngredientCount { get; set; }
    public int DirectionCount { get; set; }
    public bool HasNutrition { get; set; }

    private bool _isSaved;
    /// <summary>True when this recipe is in the user's local "My Saved" library.
    /// Not from MSSQL — populated by the view model after a list loads, and
    /// flipped live by the star toggle so the glyph updates without a reload.</summary>
    public bool IsSaved
    {
        get => _isSaved;
        set
        {
            if (_isSaved == value) return;
            _isSaved = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSaved)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

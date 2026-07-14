using System.ComponentModel;
using Microsoft.Maui.Graphics;
using IntelligentPersonalHealthOptimization.Data;

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

    /// <summary>Per-serving calories for list display. Only populated when the
    /// recipe has a real Servings value (so the figure is genuinely per-serving,
    /// not a whole-recipe total). Null otherwise — see [[project]] backfill note.</summary>
    public int? CaloriesPerServing { get; set; }

    /// <summary>Dish photo URL (Wikimedia Commons), when available (~20% of recipes).</summary>
    public string? ImageUrl { get; set; }
    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);

    /// <summary>Health-consciousness tier — "Healthy" / "Moderate" / "Indulgent"
    /// (see <see cref="Data.RecipeHealth"/>). Null if not yet classified.</summary>
    public string? HealthTier { get; set; }
    public int? HealthScore { get; set; }
    public bool IsHealthyTreat { get; set; }

    public bool IsHealthy => HealthTier == Data.RecipeHealth.Healthy;
    public bool ShowHealthBadge => Data.HealthBadgeStyle.ShouldShow(HealthTier);

    /// <summary>Badge label — "Healthy" / "Moderate" / "Indulgent", or "Treat" (a lean sweet).</summary>
    public string HealthBadgeText => Data.HealthBadgeStyle.TextFor(HealthTier, IsHealthyTreat);

    /// <summary>Badge tint, from the same palette every health surface shares.</summary>
    public Color HealthBadgeColor => Data.HealthBadgeStyle.ColorFor(HealthTier);

    // Emoji/colour fallback for the ~80% with no photo — resolved from the recipe
    // name first (e.g. "Chicken Adobo" → 🍗), then the category.
    public string Emoji => RecipeCategoryStyle.EmojiFor(
        !string.IsNullOrEmpty(RecipeName) ? RecipeName : CategoryName);
    public Color Accent => RecipeCategoryStyle.AccentFor(
        !string.IsNullOrEmpty(RecipeName) ? RecipeName : CategoryName);

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

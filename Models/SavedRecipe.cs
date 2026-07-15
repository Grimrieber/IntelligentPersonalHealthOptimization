using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

/// <summary>
/// A recipe saved locally by the user from the MSSQL cookbook.
/// Stores the full recipe data so it's available offline.
/// </summary>
public class SavedRecipe
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    /// <summary>Original RecipeID from MSSQL for duplicate detection.</summary>
    [Indexed]
    public int SourceRecipeId { get; set; }

    [Indexed, MaxLength(200)]
    public string RecipeName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PrepTime { get; set; }

    [MaxLength(50)]
    public string? CookTime { get; set; }

    [MaxLength(50)]
    public string? RestTime { get; set; }

    [MaxLength(50)]
    public string? Servings { get; set; }

    [MaxLength(20)]
    public string? Difficulty { get; set; }

    [MaxLength(200)]
    public string? Source { get; set; }

    public string? Notes { get; set; }

    public int? Rating { get; set; }
    public bool IsFavorite { get; set; }

    /// <summary>Personal 1-5 star rating the user gave this recipe after cooking
    /// it. 0 = unrated. Distinct from <see cref="Rating"/> (source-imported).</summary>
    public int MyRating { get; set; }

    /// <summary>Personal free-text notes the user attached to this recipe
    /// (tweaks, substitutions, reminders). Distinct from <see cref="Notes"/>,
    /// which holds the source recipe's own notes.</summary>
    public string? MyNote { get; set; }

    // Nutrition snapshot
    public int? CaloriesPerServing { get; set; }
    public double? ProteinGrams { get; set; }
    public double? CarbsGrams { get; set; }
    public double? FatGrams { get; set; }
    public double? FiberGrams { get; set; }
    public double? SugarGrams { get; set; }
    public double? SodiumMg { get; set; }
    public double? CholesterolMg { get; set; }
    public double? SatFatGrams { get; set; }

    /// <summary>0-100. How much of the recipe's ingredient list mapped to a
    /// known food during nutrition computation; lower = nutrition less reliable.</summary>
    public double? IngredientMatchRate { get; set; }

    [MaxLength(200)]
    public string? ServingSizeNote { get; set; }

    /// <summary>Wikimedia Commons thumbnail URL for the dish, when the source
    /// Wikibooks page has a photo (~20% of recipes). Null = no image; the UI
    /// falls back to the category emoji tile. Loaded at runtime (not bundled).</summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    // Precomputed diet-compatibility flags (classified offline from the full
    // ingredient list; see the diet classifier + DietConflictKeywords fallback).
    // Used by meal-plan generation and the swap picker so a diet can never be violated.
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public bool IsPescatarian { get; set; }
    public bool IsGlutenFree { get; set; }
    public bool IsDairyFree { get; set; }
    public bool IsKeto { get; set; }
    public bool IsPaleo { get; set; }
    public bool IsHalal { get; set; }
    public bool IsKosher { get; set; }
    public bool IsMediterranean { get; set; }

    // Precomputed health-consciousness classification (see RecipeHealth). Scored
    // on a fitness lens from the nutrition snapshot above; drives the "Healthy"
    // browse filter, the tier badge, and meal-plan junk exclusion.
    /// <summary>0-100 fitness health score; higher = leaner. Null until classified.</summary>
    public int? HealthScore { get; set; }

    /// <summary>"Healthy" / "Moderate" / "Indulgent" (see <see cref="Data.RecipeHealth"/>).</summary>
    [MaxLength(12)]
    public string? HealthTier { get; set; }

    /// <summary>True for a sweet/dessert recipe that still clears the health bar
    /// (a lean high-protein treat) — surfaced so smart treats aren't hidden.</summary>
    public bool IsHealthyTreat { get; set; }

    [MaxLength(500)]
    public string? TagsJson { get; set; }

    /// <summary>"User" for personally-saved recipes, "Wikibooks" for bundled
    /// catalog rows, future sources as added.</summary>
    [Indexed, MaxLength(50)]
    public string SourceProvider { get; set; } = "User";

    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

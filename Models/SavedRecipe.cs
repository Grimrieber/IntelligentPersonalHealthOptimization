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

    [MaxLength(500)]
    public string? TagsJson { get; set; }

    /// <summary>"User" for personally-saved recipes, "Wikibooks" for bundled
    /// catalog rows, future sources as added.</summary>
    [Indexed, MaxLength(50)]
    public string SourceProvider { get; set; } = "User";

    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

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

    [MaxLength(200)]
    public string? ServingSizeNote { get; set; }

    [MaxLength(500)]
    public string? TagsJson { get; set; }

    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

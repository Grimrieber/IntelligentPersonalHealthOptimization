namespace IntelligentPersonalHealthOptimization.Models.Recipe;

/// <summary>
/// Read-only model mapped from MSSQL RecipeDB.Recipes table.
/// </summary>
public class RecipeItem
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
}

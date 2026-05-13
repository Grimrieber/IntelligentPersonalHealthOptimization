namespace IntelligentPersonalHealthOptimization.Models.Recipe;

/// <summary>
/// Read-only model mapped from MSSQL RecipeDB.RecipeIngredients table.
/// </summary>
public class RecipeIngredient
{
    public int IngredientID { get; set; }
    public int RecipeID { get; set; }
    public int SortOrder { get; set; }
    public string? IngredientGroup { get; set; }
    public string Description { get; set; } = string.Empty;
}

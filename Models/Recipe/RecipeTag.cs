namespace IntelligentPersonalHealthOptimization.Models.Recipe;

/// <summary>
/// Read-only model mapped from MSSQL RecipeDB.RecipeTags table.
/// </summary>
public class RecipeTag
{
    public int TagID { get; set; }
    public int RecipeID { get; set; }
    public string TagName { get; set; } = string.Empty;
}

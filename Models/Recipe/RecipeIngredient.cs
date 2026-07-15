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

    // Transient: set on the recipe detail page when this line hits the user's
    // allergies / foods-to-avoid (see AllergenMatcher). Not from the DB.
    public bool AllergenFlag { get; set; }
    public string? AllergenLabel { get; set; }
}


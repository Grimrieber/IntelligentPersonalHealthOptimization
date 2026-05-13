namespace IntelligentPersonalHealthOptimization.Models.Recipe;

/// <summary>
/// Read-only model mapped from MSSQL RecipeDB.RecipeDirections table.
/// </summary>
public class RecipeDirection
{
    public int DirectionID { get; set; }
    public int RecipeID { get; set; }
    public int StepNumber { get; set; }
    public string? DirectionGroup { get; set; }
    public string Instruction { get; set; } = string.Empty;
}

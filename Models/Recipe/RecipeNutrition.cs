namespace IntelligentPersonalHealthOptimization.Models.Recipe;

/// <summary>
/// Read-only model mapped from MSSQL RecipeDB.RecipeNutrition table.
/// </summary>
public class RecipeNutrition
{
    public int NutritionID { get; set; }
    public int RecipeID { get; set; }
    public int? CaloriesPerServing { get; set; }
    public decimal? TotalFatGrams { get; set; }
    public decimal? SaturatedFatGrams { get; set; }
    public decimal? CholesterolMg { get; set; }
    public decimal? SodiumMg { get; set; }
    public decimal? TotalCarbsGrams { get; set; }
    public decimal? FiberGrams { get; set; }
    public decimal? SugarGrams { get; set; }
    public decimal? ProteinGrams { get; set; }
    public string? ServingSizeNote { get; set; }
}

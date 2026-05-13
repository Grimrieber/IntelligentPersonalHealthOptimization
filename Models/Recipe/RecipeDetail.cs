namespace IntelligentPersonalHealthOptimization.Models.Recipe;

/// <summary>
/// Composite model that holds a full recipe with all related data.
/// Used for the cookbook-style detail view.
/// </summary>
public class RecipeDetail
{
    public RecipeItem Recipe { get; set; } = new();
    public List<RecipeIngredient> Ingredients { get; set; } = [];
    public List<RecipeDirection> Directions { get; set; } = [];
    public RecipeNutrition? Nutrition { get; set; }
    public List<RecipeTag> Tags { get; set; } = [];

    /// <summary>
    /// Ingredients grouped by IngredientGroup for cookbook display.
    /// </summary>
    public List<IngredientGrouping> GroupedIngredients =>
        Ingredients
            .GroupBy(i => i.IngredientGroup ?? string.Empty)
            .Select(g => new IngredientGrouping(g.Key, g.OrderBy(i => i.SortOrder).ToList()))
            .ToList();

    /// <summary>
    /// Directions grouped by DirectionGroup for cookbook display.
    /// </summary>
    public List<DirectionGrouping> GroupedDirections =>
        Directions
            .GroupBy(d => d.DirectionGroup ?? string.Empty)
            .Select(g => new DirectionGrouping(g.Key, g.OrderBy(d => d.StepNumber).ToList()))
            .ToList();
}

public record IngredientGrouping(string GroupName, List<RecipeIngredient> Items);
public record DirectionGrouping(string GroupName, List<RecipeDirection> Steps);

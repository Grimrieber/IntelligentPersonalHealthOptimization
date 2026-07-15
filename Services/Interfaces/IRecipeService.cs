using IntelligentPersonalHealthOptimization.Models.Recipe;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

/// <summary>
/// Reads recipes from the master cookbook (MSSQL).
/// Implementation can be swapped to an API service later.
/// </summary>
public interface IRecipeService
{
    Task<List<RecipeCategory>> GetCategoriesAsync();
    Task<List<RecipeItem>> GetRecipesByCategoryAsync(int categoryId);
    Task<List<RecipeItem>> SearchRecipesAsync(string searchTerm);

    /// <summary>The entire catalog, for filtering/sorting across all categories at once.</summary>
    Task<List<RecipeItem>> GetAllRecipesAsync();

    /// <summary>Recipes similar to a given one — same category, nearest by
    /// per-serving calories — for the "More like this" strip. Excludes the
    /// source recipe itself.</summary>
    Task<List<RecipeItem>> GetSimilarRecipesAsync(int excludeId, string categoryName, int? calories, int take);

    Task<RecipeDetail?> GetRecipeDetailAsync(int recipeId);
    Task<bool> TestConnectionAsync();
}

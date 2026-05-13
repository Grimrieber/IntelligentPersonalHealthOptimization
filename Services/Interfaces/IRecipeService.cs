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
    Task<RecipeDetail?> GetRecipeDetailAsync(int recipeId);
    Task<bool> TestConnectionAsync();
}

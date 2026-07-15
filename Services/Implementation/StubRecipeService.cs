#if !WINDOWS
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

/// <summary>
/// Stub for non-Windows platforms until the REST API is built.
/// </summary>
public class StubRecipeService : IRecipeService
{
    public Task<bool> TestConnectionAsync() => Task.FromResult(false);

    public Task<List<RecipeCategory>> GetCategoriesAsync() => Task.FromResult(new List<RecipeCategory>());

    public Task<List<RecipeItem>> GetRecipesByCategoryAsync(int categoryId) => Task.FromResult(new List<RecipeItem>());

    public Task<List<RecipeItem>> SearchRecipesAsync(string searchTerm) => Task.FromResult(new List<RecipeItem>());

    public Task<List<RecipeItem>> GetAllRecipesAsync() => Task.FromResult(new List<RecipeItem>());
    public Task<List<RecipeItem>> GetSimilarRecipesAsync(int excludeId, string categoryName, int? calories, int take) => Task.FromResult(new List<RecipeItem>());

    public Task<RecipeDetail?> GetRecipeDetailAsync(int recipeId) => Task.FromResult<RecipeDetail?>(null);
}
#endif

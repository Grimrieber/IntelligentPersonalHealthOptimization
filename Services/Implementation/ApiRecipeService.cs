using System.Net.Http.Json;
using System.Text.Json;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

/// <summary>
/// HTTP-based recipe service that calls the RecipeApi project.
/// Used on Android/iOS where direct MSSQL is not available.
/// </summary>
public class ApiRecipeService : IRecipeService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiRecipeService()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(AppConstants.RecipeApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/ping");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<RecipeCategory>> GetCategoriesAsync()
    {
        var result = await _http.GetFromJsonAsync<List<RecipeCategory>>("api/categories", JsonOptions);
        return result ?? [];
    }

    public async Task<List<RecipeItem>> GetRecipesByCategoryAsync(int categoryId)
    {
        var result = await _http.GetFromJsonAsync<List<RecipeItem>>($"api/recipes/category/{categoryId}", JsonOptions);
        return result ?? [];
    }

    public async Task<List<RecipeItem>> SearchRecipesAsync(string searchTerm)
    {
        var encoded = Uri.EscapeDataString(searchTerm);
        var result = await _http.GetFromJsonAsync<List<RecipeItem>>($"api/recipes/search?q={encoded}", JsonOptions);
        return result ?? [];
    }

    public async Task<RecipeDetail?> GetRecipeDetailAsync(int recipeId)
    {
        return await _http.GetFromJsonAsync<RecipeDetail>($"api/recipes/{recipeId}", JsonOptions);
    }

}

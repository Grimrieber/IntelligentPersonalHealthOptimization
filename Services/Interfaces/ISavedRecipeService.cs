using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Recipe;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

/// <summary>
/// Manages recipes saved locally by the user (SQLite).
/// </summary>
public interface ISavedRecipeService
{
    Task<int> SaveRecipeAsync(RecipeDetail recipe, int userId);
    Task<bool> IsRecipeSavedAsync(int sourceRecipeId, int userId);
    Task RemoveSavedRecipeAsync(int sourceRecipeId, int userId);
    Task<List<SavedRecipe>> GetSavedRecipesAsync(int userId);
    Task<SavedRecipe?> GetSavedRecipeAsync(int savedRecipeId);
    Task<List<SavedRecipeIngredient>> GetSavedIngredientsAsync(int savedRecipeId);
    Task<List<SavedRecipeDirection>> GetSavedDirectionsAsync(int savedRecipeId);
    Task ToggleFavoriteAsync(int savedRecipeId);
}

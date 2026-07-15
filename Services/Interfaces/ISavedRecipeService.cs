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

    // --- Favorites ("My Saved") -------------------------------------------
    // Every cookbook recipe is already a SavedRecipe row (the bundled Wikibooks
    // catalog). "Saving" a recipe = flipping IsFavorite on its existing row;
    // the "My Saved" tab and the star toggle both run off this flag, keyed by
    // SavedRecipe.Id (which is the RecipeItem.RecipeID for catalog recipes).
    Task<bool> IsFavoriteAsync(int savedRecipeId);
    Task ToggleFavoriteAsync(int savedRecipeId);
    Task<List<SavedRecipe>> GetFavoriteRecipesAsync();

    // --- Personal rating & notes ------------------------------------------
    // Persisted on the recipe's own SavedRecipe row (MyRating / MyNote), keyed
    // by SavedRecipe.Id (== RecipeItem.RecipeID for catalog recipes).
    Task SetMyRatingAsync(int savedRecipeId, int rating);
    Task SetMyNoteAsync(int savedRecipeId, string? note);
}

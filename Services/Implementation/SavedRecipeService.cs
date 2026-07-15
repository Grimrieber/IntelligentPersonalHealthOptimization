using System.Text.Json;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

/// <summary>
/// Manages recipes saved to local SQLite/SQLCipher database.
/// </summary>
public class SavedRecipeService : ISavedRecipeService
{
    private readonly IDatabaseService _db;

    public SavedRecipeService(IDatabaseService db)
    {
        _db = db;
    }

    public async Task<int> SaveRecipeAsync(RecipeDetail recipe, int userId)
    {
        var conn = await _db.GetConnectionAsync();

        // Check if already saved
        var existing = await conn.Table<SavedRecipe>()
            .Where(r => r.SourceRecipeId == recipe.Recipe.RecipeID && r.UserId == userId)
            .FirstOrDefaultAsync();

        if (existing != null)
            return existing.Id;

        var saved = new SavedRecipe
        {
            UserId = userId,
            SourceRecipeId = recipe.Recipe.RecipeID,
            RecipeName = recipe.Recipe.RecipeName,
            CategoryName = recipe.Recipe.CategoryName,
            PrepTime = recipe.Recipe.PrepTime,
            CookTime = recipe.Recipe.CookTime,
            RestTime = recipe.Recipe.RestTime,
            Servings = recipe.Recipe.Servings,
            Difficulty = recipe.Recipe.Difficulty,
            Source = recipe.Recipe.Source,
            Notes = recipe.Recipe.Notes,
            Rating = recipe.Recipe.Rating,
            IsFavorite = recipe.Recipe.IsFavorite,
            CaloriesPerServing = recipe.Nutrition?.CaloriesPerServing,
            ProteinGrams = (double?)recipe.Nutrition?.ProteinGrams,
            CarbsGrams = (double?)recipe.Nutrition?.TotalCarbsGrams,
            FatGrams = (double?)recipe.Nutrition?.TotalFatGrams,
            FiberGrams = (double?)recipe.Nutrition?.FiberGrams,
            SugarGrams = (double?)recipe.Nutrition?.SugarGrams,
            ServingSizeNote = recipe.Nutrition?.ServingSizeNote,
            TagsJson = recipe.Tags.Count > 0
                ? JsonSerializer.Serialize(recipe.Tags.Select(t => t.TagName).ToList())
                : null,
            SavedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(saved);

        // Save ingredients
        foreach (var ingredient in recipe.Ingredients)
        {
            await conn.InsertAsync(new SavedRecipeIngredient
            {
                SavedRecipeId = saved.Id,
                SortOrder = ingredient.SortOrder,
                IngredientGroup = ingredient.IngredientGroup,
                Description = ingredient.Description
            });
        }

        // Save directions
        foreach (var direction in recipe.Directions)
        {
            await conn.InsertAsync(new SavedRecipeDirection
            {
                SavedRecipeId = saved.Id,
                StepNumber = direction.StepNumber,
                DirectionGroup = direction.DirectionGroup,
                Instruction = direction.Instruction
            });
        }

        return saved.Id;
    }

    public async Task<bool> IsRecipeSavedAsync(int sourceRecipeId, int userId)
    {
        var conn = await _db.GetConnectionAsync();
        var count = await conn.Table<SavedRecipe>()
            .Where(r => r.SourceRecipeId == sourceRecipeId && r.UserId == userId)
            .CountAsync();
        return count > 0;
    }

    public async Task RemoveSavedRecipeAsync(int sourceRecipeId, int userId)
    {
        var conn = await _db.GetConnectionAsync();
        var saved = await conn.Table<SavedRecipe>()
            .Where(r => r.SourceRecipeId == sourceRecipeId && r.UserId == userId)
            .FirstOrDefaultAsync();

        if (saved == null) return;

        // Delete ingredients and directions first
        await conn.ExecuteAsync("DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId = ?", saved.Id);
        await conn.ExecuteAsync("DELETE FROM SavedRecipeDirection WHERE SavedRecipeId = ?", saved.Id);
        await conn.DeleteAsync(saved);
    }

    public async Task<List<SavedRecipe>> GetSavedRecipesAsync(int userId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<SavedRecipe>()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.RecipeName)
            .ToListAsync();
    }

    public async Task<SavedRecipe?> GetSavedRecipeAsync(int savedRecipeId)
    {
        return await _db.GetByIdAsync<SavedRecipe>(savedRecipeId);
    }

    public async Task<List<SavedRecipeIngredient>> GetSavedIngredientsAsync(int savedRecipeId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<SavedRecipeIngredient>()
            .Where(i => i.SavedRecipeId == savedRecipeId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();
    }

    public async Task<List<SavedRecipeDirection>> GetSavedDirectionsAsync(int savedRecipeId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<SavedRecipeDirection>()
            .Where(d => d.SavedRecipeId == savedRecipeId)
            .OrderBy(d => d.StepNumber)
            .ToListAsync();
    }

    public async Task<bool> IsFavoriteAsync(int savedRecipeId)
    {
        var conn = await _db.GetConnectionAsync();
        var saved = await conn.FindAsync<SavedRecipe>(savedRecipeId);
        return saved?.IsFavorite ?? false;
    }

    public async Task ToggleFavoriteAsync(int savedRecipeId)
    {
        var conn = await _db.GetConnectionAsync();
        var saved = await conn.FindAsync<SavedRecipe>(savedRecipeId);
        if (saved == null) return;

        saved.IsFavorite = !saved.IsFavorite;
        await conn.UpdateAsync(saved);
    }

    public async Task<List<SavedRecipe>> GetFavoriteRecipesAsync()
    {
        var conn = await _db.GetConnectionAsync();
        // Raw SQL — sqlite-net's LINQ doesn't always translate bool filters well.
        return await conn.QueryAsync<SavedRecipe>(
            "SELECT * FROM SavedRecipe WHERE IsFavorite = 1 ORDER BY RecipeName");
    }

    public async Task SetMyRatingAsync(int savedRecipeId, int rating)
    {
        var conn = await _db.GetConnectionAsync();
        var saved = await conn.FindAsync<SavedRecipe>(savedRecipeId);
        if (saved == null) return;

        saved.MyRating = Math.Clamp(rating, 0, 5);
        await conn.UpdateAsync(saved);
    }

    public async Task SetMyNoteAsync(int savedRecipeId, string? note)
    {
        var conn = await _db.GetConnectionAsync();
        var saved = await conn.FindAsync<SavedRecipe>(savedRecipeId);
        if (saved == null) return;

        saved.MyNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await conn.UpdateAsync(saved);
    }
}

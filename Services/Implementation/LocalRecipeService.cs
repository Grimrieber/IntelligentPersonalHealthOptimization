using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

/// <summary>
/// Reads recipes from the on-device SQLite catalog (rows in <see cref="SavedRecipe"/>
/// seeded from the bundled Wikibooks Cookbook). Returns the same shapes the
/// SQL/API-backed implementations did, so ViewModels don't need to change.
/// </summary>
public class LocalRecipeService : IRecipeService
{
    private readonly IDatabaseService _db;
    private const string BundledSource = "Wikibooks";

    public LocalRecipeService(IDatabaseService db)
    {
        _db = db;
    }

    public async Task<List<RecipeCategory>> GetCategoriesAsync()
    {
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<SavedRecipe>()
            .Where(r => r.SourceProvider == BundledSource)
            .ToListAsync();

        return rows
            .GroupBy(r => r.CategoryName ?? "Uncategorized")
            .OrderBy(g => g.Key)
            .Select(g => new RecipeCategory
            {
                CategoryID = StableCategoryId(g.Key),
                CategoryName = g.Key,
                RecipeCount = g.Count(),
            })
            .ToList();
    }

    public async Task<List<RecipeItem>> GetRecipesByCategoryAsync(int categoryId)
    {
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<SavedRecipe>()
            .Where(r => r.SourceProvider == BundledSource)
            .OrderBy(r => r.RecipeName)
            .ToListAsync();

        var inCategory = rows
            .Where(r => StableCategoryId(r.CategoryName ?? "Uncategorized") == categoryId)
            .ToList();

        return await MapWithCountsAsync(conn, inCategory);
    }

    public async Task<List<RecipeItem>> SearchRecipesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<RecipeItem>();

        var conn = await _db.GetConnectionAsync();
        // Use raw SQL with explicit LIKE + COLLATE NOCASE — sqlite-net's LINQ
        // translation of .Contains() in a compound Where is unreliable.
        var like = "%" + searchTerm.Trim() + "%";
        var rows = await conn.QueryAsync<SavedRecipe>(
            "SELECT * FROM SavedRecipe " +
            "WHERE SourceProvider = ? AND RecipeName LIKE ? COLLATE NOCASE " +
            "ORDER BY RecipeName LIMIT 200",
            BundledSource, like);

        return await MapWithCountsAsync(conn, rows);
    }

    /// <summary>Maps SavedRecipe rows to RecipeItem with IngredientCount /
    /// DirectionCount populated via two batched COUNT(*) GROUP BY queries
    /// (vs N+1 per-recipe lookups).</summary>
    private static async Task<List<RecipeItem>> MapWithCountsAsync(
        SQLite.SQLiteAsyncConnection conn,
        List<SavedRecipe> recipes)
    {
        if (recipes.Count == 0)
            return new List<RecipeItem>();

        var ids = recipes.Select(r => r.Id).ToList();
        var ingCounts = await CountByRecipeIdAsync(conn, "SavedRecipeIngredient", ids);
        var dirCounts = await CountByRecipeIdAsync(conn, "SavedRecipeDirection", ids);

        return recipes.Select(r =>
        {
            var item = MapToRecipeItem(r);
            item.IngredientCount = ingCounts.GetValueOrDefault(r.Id, 0);
            item.DirectionCount = dirCounts.GetValueOrDefault(r.Id, 0);
            return item;
        }).ToList();
    }

    private static async Task<Dictionary<int, int>> CountByRecipeIdAsync(
        SQLite.SQLiteAsyncConnection conn, string tableName, List<int> recipeIds)
    {
        var placeholders = string.Join(",", Enumerable.Repeat("?", recipeIds.Count));
        var sql =
            $"SELECT SavedRecipeId, COUNT(*) AS Cnt FROM {tableName} " +
            $"WHERE SavedRecipeId IN ({placeholders}) GROUP BY SavedRecipeId";
        var rows = await conn.QueryAsync<CountRow>(sql, recipeIds.Cast<object>().ToArray());
        return rows.ToDictionary(r => r.SavedRecipeId, r => r.Cnt);
    }

    private class CountRow
    {
        public int SavedRecipeId { get; set; }
        public int Cnt { get; set; }
    }

    public async Task<RecipeDetail?> GetRecipeDetailAsync(int recipeId)
    {
        var conn = await _db.GetConnectionAsync();
        var saved = await conn.FindAsync<SavedRecipe>(recipeId);
        if (saved == null || saved.SourceProvider != BundledSource)
            return null;

        var ingredients = await conn.Table<SavedRecipeIngredient>()
            .Where(i => i.SavedRecipeId == saved.Id)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        var directions = await conn.Table<SavedRecipeDirection>()
            .Where(d => d.SavedRecipeId == saved.Id)
            .OrderBy(d => d.StepNumber)
            .ToListAsync();

        return new RecipeDetail
        {
            Recipe = MapToRecipeItem(saved),
            Ingredients = ingredients
                .Select(i => new RecipeIngredient
                {
                    IngredientID = i.Id,
                    RecipeID = saved.Id,
                    SortOrder = i.SortOrder,
                    IngredientGroup = i.IngredientGroup,
                    Description = i.Description,
                })
                .ToList(),
            Directions = directions
                .Select(d => new RecipeDirection
                {
                    DirectionID = d.Id,
                    RecipeID = saved.Id,
                    StepNumber = d.StepNumber,
                    DirectionGroup = d.DirectionGroup,
                    Instruction = d.Instruction,
                })
                .ToList(),
            Nutrition = saved.CaloriesPerServing.HasValue || saved.ProteinGrams.HasValue
                ? new RecipeNutrition
                {
                    NutritionID = saved.Id,
                    RecipeID = saved.Id,
                    CaloriesPerServing = saved.CaloriesPerServing,
                    ProteinGrams = ToDec(saved.ProteinGrams),
                    TotalCarbsGrams = ToDec(saved.CarbsGrams),
                    TotalFatGrams = ToDec(saved.FatGrams),
                    FiberGrams = ToDec(saved.FiberGrams),
                    SugarGrams = ToDec(saved.SugarGrams),
                    SodiumMg = ToDec(saved.SodiumMg),
                    CholesterolMg = ToDec(saved.CholesterolMg),
                    SaturatedFatGrams = ToDec(saved.SatFatGrams),
                    ServingSizeNote = saved.ServingSizeNote,
                }
                : null,
            Tags = new List<RecipeTag>(),
        };
    }

    public async Task<bool> TestConnectionAsync()
    {
        var conn = await _db.GetConnectionAsync();
        var count = await conn.Table<SavedRecipe>()
            .Where(r => r.SourceProvider == BundledSource)
            .CountAsync();
        return count > 0;
    }

    private static RecipeItem MapToRecipeItem(SavedRecipe r)
    {
        var catName = r.CategoryName ?? "Uncategorized";
        return new RecipeItem
        {
            RecipeID = r.Id,
            CategoryID = StableCategoryId(catName),
            CategoryName = catName,
            RecipeName = r.RecipeName,
            PrepTime = r.PrepTime,
            CookTime = r.CookTime,
            RestTime = r.RestTime,
            Servings = r.Servings,
            Difficulty = r.Difficulty,
            Source = r.Source,
            Notes = r.Notes,
            RecipeImage = null,
            ImageUrl = r.ImageUrl,
            DateAdded = r.SavedAt,
            LastMadeOn = null,
            Rating = r.Rating,
            IsFavorite = r.IsFavorite,
            HasNutrition = r.CaloriesPerServing.HasValue,
            HealthTier = r.HealthTier,
            HealthScore = r.HealthScore,
            IsHealthyTreat = r.IsHealthyTreat,
            // Per-serving macros + diet flags for cookbook filters/sort.
            ProteinGrams = r.ProteinGrams,
            CarbsGrams = r.CarbsGrams,
            FatGrams = r.FatGrams,
            FiberGrams = r.FiberGrams,
            SugarGrams = r.SugarGrams,
            IsVegetarian = r.IsVegetarian,
            IsVegan = r.IsVegan,
            IsPescatarian = r.IsPescatarian,
            IsGlutenFree = r.IsGlutenFree,
            IsDairyFree = r.IsDairyFree,
            IsKeto = r.IsKeto,
            IsPaleo = r.IsPaleo,
            IsHalal = r.IsHalal,
            IsKosher = r.IsKosher,
            IsMediterranean = r.IsMediterranean,
            // Only surface per-serving calories on cards when a real Servings
            // value exists AND the figure is a real positive number — a 0/null
            // means nutrition couldn't be computed, so don't show "0 cal/serving".
            CaloriesPerServing =
                (!string.IsNullOrWhiteSpace(r.Servings) && r.CaloriesPerServing is > 0)
                    ? r.CaloriesPerServing : null,
            // IngredientCount / DirectionCount are not stored on SavedRecipe;
            // a 0 here is fine for list views (they don't render the count).
        };
    }

    // Deterministic int hash for category names so the same category gets the
    // same CategoryID across calls (ViewModels pass it back to GetRecipesByCategoryAsync).
    private static int StableCategoryId(string name)
    {
        unchecked
        {
            int hash = 23;
            foreach (var c in name)
                hash = hash * 31 + c;
            // Ensure positive
            return hash & 0x7FFFFFFF;
        }
    }

    private static decimal? ToDec(double? v) => v.HasValue ? (decimal)v.Value : null;
}

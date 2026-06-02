using System.IO.Compression;
using System.Text.Json;
using IntelligentPersonalHealthOptimization.Models;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Data;

public static partial class SeedData
{
    private const string WikibooksBundleAsset = "wikibooks_bundle.json.gz";
    private const string WikibooksSourceProvider = "Wikibooks";

    /// <summary>
    /// Seeds the SavedRecipe / SavedRecipeIngredient / SavedRecipeDirection
    /// tables from the bundled Wikibooks Cookbook gzip JSON the first time
    /// the app launches. Skips if any Wikibooks-sourced rows already exist.
    /// </summary>
    public static async Task SeedWikibooksRecipesAsync(SQLiteAsyncConnection connection)
    {
        var already = await connection.Table<SavedRecipe>()
            .Where(r => r.SourceProvider == WikibooksSourceProvider)
            .CountAsync();
        if (already > 0)
            return;

        WikibooksBundle? bundle;
        try
        {
            using var asset = await FileSystem.OpenAppPackageFileAsync(WikibooksBundleAsset);
            using var gz = new GZipStream(asset, CompressionMode.Decompress);
            bundle = await JsonSerializer.DeserializeAsync<WikibooksBundle>(
                gz,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (FileNotFoundException)
        {
            // Asset missing — silent skip rather than crashing the app.
            return;
        }

        if (bundle?.Recipes == null || bundle.Recipes.Count == 0)
            return;

        var recipes = new List<SavedRecipe>(bundle.Recipes.Count);
        foreach (var r in bundle.Recipes)
        {
            var nut = r.Nutrition;
            recipes.Add(new SavedRecipe
            {
                UserId = 0,                                  // shared catalog, not per-user
                SourceRecipeId = 0,                          // no upstream ID for Wikibooks
                RecipeName = Trunc(r.Name, 200) ?? string.Empty,
                CategoryName = Trunc(r.Category ?? "Uncategorized", 100) ?? "Uncategorized",
                PrepTime = Trunc(r.PrepTime, 50),
                CookTime = Trunc(r.CookTime, 50),
                RestTime = Trunc(r.RestTime, 50),
                Servings = Trunc(r.Servings, 50),
                Difficulty = Trunc(r.Difficulty, 20),
                Source = Trunc(r.Source, 200),
                Notes = r.Notes,
                Rating = null,
                IsFavorite = false,
                CaloriesPerServing = nut?.CaloriesPerServing,
                ProteinGrams = nut?.ProteinGrams,
                CarbsGrams = nut?.CarbsGrams,
                FatGrams = nut?.FatGrams,
                FiberGrams = nut?.FiberGrams,
                SugarGrams = nut?.SugarGrams,
                SodiumMg = nut?.SodiumMg,
                CholesterolMg = nut?.CholesterolMg,
                SatFatGrams = nut?.SatFatGrams,
                IngredientMatchRate = nut?.MatchRate,
                ServingSizeNote = Trunc(nut?.ServingSizeNote, 200),
                SourceProvider = WikibooksSourceProvider,
                SavedAt = DateTime.UtcNow,
            });
        }

        // Insert recipes first so we have the auto-generated Ids.
        await connection.InsertAllAsync(recipes);

        // Now build ingredient + direction child rows keyed to the inserted Ids.
        var ingredients = new List<SavedRecipeIngredient>(bundle.Recipes.Sum(r => r.Ingredients?.Count ?? 0));
        var directions  = new List<SavedRecipeDirection> (bundle.Recipes.Sum(r => r.Directions?.Count  ?? 0));

        for (int i = 0; i < bundle.Recipes.Count; i++)
        {
            var src = bundle.Recipes[i];
            var savedId = recipes[i].Id;

            if (src.Ingredients != null)
            {
                foreach (var ing in src.Ingredients)
                {
                    ingredients.Add(new SavedRecipeIngredient
                    {
                        SavedRecipeId = savedId,
                        SortOrder = ing.Order,
                        IngredientGroup = Trunc(ing.Group, 200),
                        Description = Trunc(ing.Description, 500) ?? string.Empty,
                    });
                }
            }

            if (src.Directions != null)
            {
                foreach (var dir in src.Directions)
                {
                    directions.Add(new SavedRecipeDirection
                    {
                        SavedRecipeId = savedId,
                        StepNumber = dir.Step,
                        DirectionGroup = Trunc(dir.Group, 200),
                        Instruction = dir.Instruction ?? string.Empty,
                    });
                }
            }
        }

        if (ingredients.Count > 0)
            await connection.InsertAllAsync(ingredients);
        if (directions.Count > 0)
            await connection.InsertAllAsync(directions);
    }

    private static string? Trunc(string? s, int max)
        => s == null ? null : (s.Length <= max ? s : s.Substring(0, max));

    // ----- Bundle DTOs (matches Tools/wikibooks_export_bundle.py output) -----

    private sealed class WikibooksBundle
    {
        public int SchemaVersion { get; set; }
        public string? Source { get; set; }
        public string? License { get; set; }
        public string? SourceProvider { get; set; }
        public List<string>? Categories { get; set; }
        public List<WikibooksRecipeDto> Recipes { get; set; } = new();
    }

    private sealed class WikibooksRecipeDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? PrepTime { get; set; }
        public string? CookTime { get; set; }
        public string? RestTime { get; set; }
        public string? Servings { get; set; }
        public string? Difficulty { get; set; }
        public string? Source { get; set; }
        public string? Notes { get; set; }
        public List<WikibooksIngredientDto>? Ingredients { get; set; }
        public List<WikibooksDirectionDto>? Directions { get; set; }
        public WikibooksNutritionDto? Nutrition { get; set; }
    }

    private sealed class WikibooksIngredientDto
    {
        public int Order { get; set; }
        public string? Group { get; set; }
        public string? Description { get; set; }
    }

    private sealed class WikibooksDirectionDto
    {
        public int Step { get; set; }
        public string? Group { get; set; }
        public string? Instruction { get; set; }
    }

    private sealed class WikibooksNutritionDto
    {
        public int? CaloriesPerServing { get; set; }
        public double? ProteinGrams { get; set; }
        public double? CarbsGrams { get; set; }
        public double? FatGrams { get; set; }
        public double? FiberGrams { get; set; }
        public double? SodiumMg { get; set; }
        public double? CholesterolMg { get; set; }
        public double? SatFatGrams { get; set; }
        public double? SugarGrams { get; set; }
        public string? ServingSizeNote { get; set; }
        public double? MatchRate { get; set; }
    }
}

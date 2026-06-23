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

    // Bump suffix to re-run after a future bundle refresh.
    private const string ServingsBackfillMarker = "servings_backfill_2026_06_23_v1.done";

    /// <summary>
    /// One-shot: refresh Servings + per-serving nutrition on existing catalog
    /// rows from the (re-exported) bundle. The original bundle shipped ~2,600
    /// recipes without servings (nutrition stored as whole-recipe totals); the
    /// new bundle has estimated servings and divided per-serving values. Matches
    /// by RecipeName and only updates the nutrition/servings fields, so favorites
    /// (IsFavorite), ratings, and saved-at timestamps are preserved. New installs
    /// get the correct values straight from the seeder and skip this. Self-gated.
    /// </summary>
    public static async Task ApplyServingsBackfillAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, ServingsBackfillMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            WikibooksBundle? bundle;
            using (var asset = await FileSystem.OpenAppPackageFileAsync(WikibooksBundleAsset))
            using (var gz = new GZipStream(asset, CompressionMode.Decompress))
            {
                bundle = await JsonSerializer.DeserializeAsync<WikibooksBundle>(
                    gz, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            if (bundle?.Recipes == null || bundle.Recipes.Count == 0)
                return;

            var byName = new Dictionary<string, WikibooksRecipeDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in bundle.Recipes)
                byName[r.Name] = r;   // dup names → same recipe, last wins is fine

            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);

            // Apply all row updates in a single transaction so ~3,900 writes
            // don't fsync individually and stall first launch.
            await connection.RunInTransactionAsync(conn =>
            {
                foreach (var row in rows)
                {
                    if (!byName.TryGetValue(row.RecipeName, out var src))
                        continue;

                    row.Servings = Trunc(src.Servings, 50);

                    var nut = src.Nutrition;
                    if (nut != null)
                    {
                        row.CaloriesPerServing = nut.CaloriesPerServing;
                        row.ProteinGrams = nut.ProteinGrams;
                        row.CarbsGrams = nut.CarbsGrams;
                        row.FatGrams = nut.FatGrams;
                        row.FiberGrams = nut.FiberGrams;
                        row.SugarGrams = nut.SugarGrams;
                        row.SodiumMg = nut.SodiumMg;
                        row.CholesterolMg = nut.CholesterolMg;
                        row.SatFatGrams = nut.SatFatGrams;
                        row.ServingSizeNote = Trunc(nut.ServingSizeNote, 200);
                    }

                    conn.Update(row);
                }
            });

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    // Bump suffix to re-run after a future bundle refresh.
    // v2: second audit wave — drop 11 no-direction recipes + strip leaked wiki junk.
    // v3: refresh precomputed diet flags (IsVegan/IsVegetarian/...) from the bundle.
    // v4: refined classifier (drop "gravy" false-positive, honor vegan/vegetarian
    //     qualifiers + goat-cheese/duck-egg dairy remap).
    // v5: exhaustive audit — meat-fats (lard/suet/tallow) + fish condiments now block
    //     vegetarian/pescatarian correctly (closed 44 leaks). 0 leaks across all diets.
    // v6: perfection pass — added organ/cured meats (liver/guanciale), named cheeses
    //     (parmesan/feta/pecorino), crème, matzo/farina, composite cakes. Verified by
    //     Tools/wikibooks_diet_audit.py: 0 leaks + invariants hold on the shipped bundle.
    // v7: added 4 more diets — IsKeto/IsPaleo/IsHalal/IsKosher.
    // v8: added IsMediterranean (no red/processed meat).
    // v9: category-based classification + expanded fish/meat keywords — fixes on-device
    //     leaks (tilapia/filet-mignon/hot-dog flagged vegetarian). Audit gains a category oracle.
    // v10: ambiguous sausage/hot-dog family treated as pork-risk for halal/kosher unless
    //      qualified (beef/chicken/etc.) — "sage-flavored sausage" no longer halal.
    // v11: full-pool audit (every recipe, not a sample) — iguana now meat, chicken-wing dishes
    //      caught, "ale" is alcohol (halal), scaleless fish (eel) excluded from kosher.
    // v12: removed 387 non-meal recipes from the bundle (drinks, sauces, spice mixes, etc.)
    //      to save space — migration deletes them on-device. Smoothies/shakes kept.
    private const string RecipeAuditFixMarker = "recipe_audit_fix_2026_06_23_v12.done";

    /// <summary>
    /// One-shot audit cleanup: the re-exported bundle drops ~642 recipes whose
    /// nutrition couldn't be computed (unquantified ingredients) and re-estimates
    /// servings for implausible ones. This (a) removes on-device catalog rows no
    /// longer in the bundle and (b) refreshes servings/nutrition for the rest.
    /// New installs seed straight from the clean bundle and skip this. Self-gated.
    /// </summary>
    public static async Task ApplyRecipeAuditFixAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, RecipeAuditFixMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            WikibooksBundle? bundle;
            using (var asset = await FileSystem.OpenAppPackageFileAsync(WikibooksBundleAsset))
            using (var gz = new GZipStream(asset, CompressionMode.Decompress))
            {
                bundle = await JsonSerializer.DeserializeAsync<WikibooksBundle>(
                    gz, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            if (bundle?.Recipes == null || bundle.Recipes.Count == 0)
                return;

            var byName = new Dictionary<string, WikibooksRecipeDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in bundle.Recipes)
                byName[r.Name] = r;

            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);

            await connection.RunInTransactionAsync(conn =>
            {
                foreach (var row in rows)
                {
                    if (!byName.TryGetValue(row.RecipeName, out var src))
                    {
                        // No longer in the clean bundle → audit-excluded. Remove it.
                        conn.Delete(row);
                        continue;
                    }

                    row.Servings = Trunc(src.Servings, 50);
                    row.IsVegetarian = src.IsVegetarian;
                    row.IsVegan = src.IsVegan;
                    row.IsPescatarian = src.IsPescatarian;
                    row.IsGlutenFree = src.IsGlutenFree;
                    row.IsDairyFree = src.IsDairyFree;
                    row.IsKeto = src.IsKeto;
                    row.IsPaleo = src.IsPaleo;
                    row.IsHalal = src.IsHalal;
                    row.IsKosher = src.IsKosher;
                    row.IsMediterranean = src.IsMediterranean;
                    var nut = src.Nutrition;
                    if (nut != null)
                    {
                        row.CaloriesPerServing = nut.CaloriesPerServing;
                        row.ProteinGrams = nut.ProteinGrams;
                        row.CarbsGrams = nut.CarbsGrams;
                        row.FatGrams = nut.FatGrams;
                        row.FiberGrams = nut.FiberGrams;
                        row.SugarGrams = nut.SugarGrams;
                        row.SodiumMg = nut.SodiumMg;
                        row.CholesterolMg = nut.CholesterolMg;
                        row.SatFatGrams = nut.SatFatGrams;
                        row.ServingSizeNote = Trunc(nut.ServingSizeNote, 200);
                    }
                    conn.Update(row);
                }
            });

            // Sweep child rows orphaned by the deletions above.
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeDirection WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");

            // Strip leaked wiki edit-link junk from category names
            // (e.g. 'Flatbread recipes&action=edit&redlink=1'). Done directly on
            // CategoryName rather than from the bundle so recategorized recipes
            // (Uncategorized → inferred) aren't reverted.
            await connection.ExecuteAsync(
                "UPDATE SavedRecipe SET CategoryName = TRIM(SUBSTR(CategoryName, 1, INSTR(CategoryName,'&')-1)) " +
                "WHERE SourceProvider = ? AND INSTR(CategoryName,'&') > 0", WikibooksSourceProvider);

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    // Marker for the non-meal purge below.
    private const string NonMealPurgeMarker = "non_meal_purge_2026_06_23_v1.done";

    /// <summary>
    /// One-shot: delete catalog recipes that aren't meals — drinks (beverages,
    /// cocktails, juice, wine) and pure components (sauces, dressings, marinades,
    /// spice mixes, syrups, jams, stocks). Smoothies/shakes are kept. Purges by
    /// category via <see cref="RecipeCategoryGroups.IsMealPlanEligible"/>, so it is
    /// deterministic and works even if the bundled asset is stale. Frees DB space.
    /// Self-gated; runs once.
    /// </summary>
    public static async Task ApplyNonMealPurgeAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, NonMealPurgeMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = ?", WikibooksSourceProvider);
            var toDelete = rows
                .Where(r => !RecipeCategoryGroups.IsMealPlanEligible(r.CategoryName))
                .ToList();

            if (toDelete.Count > 0)
            {
                await connection.RunInTransactionAsync(conn =>
                {
                    foreach (var row in toDelete)
                        conn.Delete(row);
                });
                // Sweep child rows orphaned by the deletions.
                await connection.ExecuteAsync(
                    "DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
                await connection.ExecuteAsync(
                    "DELETE FROM SavedRecipeDirection WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
            }

            File.WriteAllText(markerPath, $"purged {toDelete.Count} non-meal recipes {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — meal-plan filtering already excludes these at query time.
        }
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
        public bool IsVegetarian { get; set; }
        public bool IsVegan { get; set; }
        public bool IsPescatarian { get; set; }
        public bool IsGlutenFree { get; set; }
        public bool IsDairyFree { get; set; }
        public bool IsKeto { get; set; }
        public bool IsPaleo { get; set; }
        public bool IsHalal { get; set; }
        public bool IsKosher { get; set; }
        public bool IsMediterranean { get; set; }
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

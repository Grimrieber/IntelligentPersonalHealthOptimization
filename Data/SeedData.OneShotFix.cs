using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Data;

public static partial class SeedData
{
    // TEMPORARY one-shot fix marker. Bump the suffix if we need another fix wave.
    private const string OneShotMarker = "fix_2026_06_01_v4.done";

    /// <summary>
    /// One-shot DB correction applied on next launch only.
    /// v2: Align every stored value EXACTLY with the Excel calculator's inputs
    /// (210 lb, 5'10", 185 lb target) so the displayed Target Calories number
    /// matches Excel/Calculator.net to the kcal.
    /// Self-gated by a marker file so it runs exactly once.
    /// </summary>
    public static async Task ApplyOneShotProfileFixAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, OneShotMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            var user = await connection.Table<User>().FirstOrDefaultAsync();
            if (user != null)
            {
                // Match Excel inputs EXACTLY:
                //   Weight = 210 lb / 2.20462 = 95.25451098148433 kg
                //   Height = 5'10" = 70 in × 2.54 = 177.80 cm
                //   ActivityLevel = ModeratelyActive
                user.WeightKg       = 210.0 / 2.20462;     // 95.2545 kg
                user.HeightCm       = 70.0  * 2.54;        // 177.80 cm
                user.ActivityLevel  = ActivityLevel.ModeratelyActive;
                user.UpdatedAt      = DateTime.UtcNow;
                await connection.UpdateAsync(user);

                // Match Excel target: 185 lb / 2.20462 = 83.91468824559335 kg
                var latest = await connection.Table<NutritionAssessment>()
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.AssessmentDate)
                    .FirstOrDefaultAsync();
                if (latest != null)
                {
                    latest.TargetWeightKg = 200.0 / 2.20462;   // 90.7185 kg — matches Excel v3
                    await connection.UpdateAsync(latest);
                }
            }

            // 3) Clean leaked Wikibooks edit-link URL params out of CategoryName
            //    (e.g. 'American dessert recipes&action=edit&redlink=1')
            await connection.ExecuteAsync(
                "UPDATE SavedRecipe SET CategoryName = TRIM(SUBSTR(CategoryName, 1, INSTR(CategoryName,'&')-1)) " +
                "WHERE INSTR(CategoryName,'&') > 0");
            await connection.ExecuteAsync(
                "UPDATE SavedRecipe SET CategoryName = TRIM(SUBSTR(CategoryName, 1, INSTR(CategoryName,'?')-1)) " +
                "WHERE INSTR(CategoryName,'?') > 0");

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort fix — don't crash app startup if it fails
        }
    }

    // Separate marker so this runs once independently of the profile fix above
    // (bumping that marker would re-reset the user's profile to the Excel values).
    private const string RecipeCleanupMarker = "recipe_cleanup_2026_06_01_v1.done";

    /// <summary>
    /// One-shot removal of unusable bundled recipes flagged by the soundness
    /// audit: rows with NO ingredients or NO directions (~88 recipes), plus
    /// scraped wiki meta-pages (templates / "Example category"). Favorites and
    /// every complete recipe are untouched. Self-gated; runs once.
    /// </summary>
    public static async Task ApplyRecipeCleanupAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, RecipeCleanupMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            // Delete the unusable recipes first. The subqueries read the child
            // tables, which are still intact here, so the "missing ingredients /
            // directions" test is evaluated against the original data.
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipe WHERE SourceProvider = 'Wikibooks' AND (" +
                "Id NOT IN (SELECT SavedRecipeId FROM SavedRecipeIngredient) OR " +
                "Id NOT IN (SELECT SavedRecipeId FROM SavedRecipeDirection))");

            // Scraped wiki meta-pages that slipped into the catalog.
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipe WHERE SourceProvider = 'Wikibooks' AND (" +
                "RecipeName LIKE '%template%' OR RecipeName LIKE 'Policy/%' OR " +
                "RecipeName = 'Example category')");

            // Sweep child rows orphaned by the deletions above.
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeDirection WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    private const string RecategorizeMarker = "recipe_recategorize_2026_06_01_v1.done";

    /// <summary>
    /// One-shot: ~118 bundled recipes have CategoryName "Uncategorized" (no
    /// category in the source). Infer a real category from the recipe NAME
    /// (cuisine adjective, or a course/ingredient keyword) so they drill into a
    /// sensible group instead of "Other". Names we can't read confidently stay
    /// Uncategorized. Self-gated; runs once.
    /// </summary>
    public static async Task ApplyRecipeRecategorizeAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, RecategorizeMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            var rows = await connection.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = 'Wikibooks' AND CategoryName = 'Uncategorized'");

            foreach (var r in rows)
            {
                var cat = CategoryFromName(r.RecipeName);
                if (cat == null) continue;
                r.CategoryName = cat;
                await connection.UpdateAsync(r);
            }

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    // Cuisine adjective in a recipe name → "<Cuisine> recipes" (checked first).
    private static readonly (string key, string cat)[] NameCuisines =
    {
        ("albanian", "Albanian recipes"), ("aztec", "Mexican recipes"), ("senegalese", "Senegalese recipes"),
        ("turkish", "Turkish recipes"), ("cameroon", "Cameroonian recipes"), ("mexican", "Mexican recipes"),
        ("malaysian", "Malaysian recipes"), ("alsatian", "French recipes"), ("egyptian", "Egyptian recipes"),
        ("thai", "Thai recipes"), ("greek", "Greek recipes"), ("maltese", "Maltese recipes"),
        ("nigerian", "Nigerian recipes"), ("ghanian", "Ghanaian recipes"), ("ghanaian", "Ghanaian recipes"),
        ("puerto", "Puerto Rican recipes"), ("swedish", "Swedish recipes"), ("gambian", "Gambian recipes"),
        ("danish", "Danish recipes"), ("finnish", "Finnish recipes"), ("yemenite", "Jewish recipes"),
        ("australian", "Australian recipes"), ("italian", "Italian recipes"), ("chinese", "Chinese recipes"),
        ("korean", "Korean recipes"), ("japanese", "Japanese recipes"), ("pakistani", "Pakistani recipes"),
        ("indian", "Indian recipes"),
    };

    /// <summary>Best-guess category for an uncategorized recipe from its name,
    /// or null to leave it Uncategorized. Mirrors the audited Python heuristic.</summary>
    private static string? CategoryFromName(string? recipeName)
    {
        var n = (recipeName ?? string.Empty).ToLowerInvariant();
        if (n.Length == 0) return null;

        foreach (var (key, cat) in NameCuisines)
            if (n.Contains(key)) return cat;

        bool Has(params string[] ks) { foreach (var k in ks) if (n.Contains(k)) return true; return false; }

        if (Has("soup", "chowder", "bisque", "gumbo")) return "Soup recipes";
        if (Has("stew")) return "Stew recipes";
        if (Has("chili")) return "Chili recipes";
        if (Has("salad")) return "Salad recipes";
        if (Has("pie", "tart", "cobbler")) return "Pie recipes";
        if (Has("cake", "cupcake")) return "Cake recipes";
        if (Has("cookie", "biscotti")) return "Cookie recipes";
        if (Has("pudding", "custard", "mousse", "confection", "rocky road", "charoset", "chocolate",
                "donut", "doughnut", "loukoum", "pastry", "kanafeh", "crust", "crème", "creme")) return "Dessert recipes";
        if (Has("granola", "müsli", "musli", "breakfast", "grits", "frittata", "æbleskiver",
                "ebleskiver", "pancake")) return "Breakfast recipes";
        if (Has("bread", "cornbread", "baguette", "sourdough", "bagel")) return "Bread recipes";
        if (Has("flour", "baking mix", "self-rising", "seitan")) return "Baking recipes";
        if (Has("mead", "sima")) return "Beverage recipes";
        if (Has("butter", "oil", "chutney", "paste", "puree", "slurry", "wash", "jelly", "jam",
                "stuffing", "filling", "sofrito", "tapenade", "mincemeat", "sauerkraut", "stock", "sauce", "spread")) return "Sauce recipes";
        if (Has("nacho", "chips", "crouton", "bruschetta", "nugget", "chin chin", "pakoda",
                "olive ascolane", "turds", "toast")) return "Appetizer recipes";
        if (Has("pizza")) return "Pizza recipes";
        if (Has("pasta", "spaghetti", "macaroni", "fleischnacka")) return "Pasta recipes";
        if (Has("rice", "jollof", "tuo")) return "Rice recipes";
        if (Has("chicken")) return "Chicken recipes";
        if (Has("beef", "meatball", "shawarma", "qimah", "minced meat")) return "Beef recipes";
        if (Has("pork", "bacon", "ham ")) return "Pork recipes";
        if (Has("salmon", "tuna", "fish")) return "Seafood recipes";
        if (Has("burrito", "egg roll", "chalupa", "falafel", "dolma", "dabeli", "burek", "börek",
                "shakshuka", "grape leaves", "stuffed")) return "Main course recipes";
        if (Has("burger", "tofu", "lentil")) return "Vegetarian recipes";
        if (Has("plantain", "yam", "potato", "greens", "okra", "cucumber", "vegetable",
                "bell pepper", "collard", "pumpkin", "corn", "bean")) return "Vegetable recipes";
        if (Has("egg", "grits")) return "Breakfast recipes";

        return null;
    }

    private const string DuplicateCleanupMarker = "dup_user_recipe_cleanup_2026_06_01_v1.done";

    /// <summary>
    /// One-shot purge of duplicate <c>SourceProvider="User"</c> recipe rows the old
    /// meal-plan pool builder created (it re-saved every catalog recipe as a user
    /// copy). The catalog ("Wikibooks") rows and the IsFavorite flag are untouched.
    /// Self-gated; runs once.
    /// </summary>
    public static async Task ApplyDuplicateRecipeCleanupAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, DuplicateCleanupMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            await connection.ExecuteAsync("DELETE FROM SavedRecipe WHERE SourceProvider = 'User'");
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeDirection WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    private const string TrainingProfileDedupMarker = "training_profile_dedup_2026_06_01_v1.done";

    /// <summary>
    /// One-shot: collapse duplicate TrainingProfile rows (onboarding + the equipment
    /// wizard + level changes could each create one), keeping the most recent per user
    /// so the level the UI shows/sets matches the row the generator reads. Self-gated.
    /// </summary>
    public static async Task ApplyTrainingProfileDedupAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, TrainingProfileDedupMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            await connection.ExecuteAsync(
                "DELETE FROM TrainingProfile WHERE Id NOT IN " +
                "(SELECT MAX(Id) FROM TrainingProfile GROUP BY UserId)");
            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }

    private const string QuestionableRemovalMarker = "recipe_remove_questionable_2026_06_01_v1.done";

    // Inappropriate / non-food recipes flagged by the validity audit and
    // confirmed for removal. Matched by exact name so other recipes sharing a
    // category (e.g. "Meat recipes") are untouched.
    private static readonly string[] QuestionableRecipeNames =
    {
        "Placenta Stew", "Placenta with Broccoli", "Spicy Australian Placenta",
        "Grilled Dog", "Dog Biscuits", "Cannabutter",
        "Iguana Pozole", "Roast Iguana with Birria Marinade",
    };

    /// <summary>One-shot removal of audited non-food / inappropriate recipes
    /// (human placenta, dog meat & pet food, cannabis, iguana). Self-gated.</summary>
    public static async Task ApplyQuestionableRecipeRemovalAsync(SQLiteAsyncConnection connection)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, QuestionableRemovalMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            var placeholders = string.Join(",", QuestionableRecipeNames.Select(_ => "?"));
            var args = QuestionableRecipeNames.Cast<object>().ToArray();

            await connection.ExecuteAsync(
                $"DELETE FROM SavedRecipe WHERE SourceProvider = 'Wikibooks' AND RecipeName IN ({placeholders})",
                args);

            // Sweep child rows orphaned by the deletions.
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeIngredient WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");
            await connection.ExecuteAsync(
                "DELETE FROM SavedRecipeDirection WHERE SavedRecipeId NOT IN (SELECT Id FROM SavedRecipe)");

            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't crash app startup if it fails.
        }
    }
}

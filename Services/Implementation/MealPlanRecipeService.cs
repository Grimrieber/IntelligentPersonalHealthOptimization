using System.Text.Json;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class MealPlanRecipeService : IMealPlanRecipeService
{
    private readonly IRecipeService _recipeService;
    private readonly ISavedRecipeService _savedRecipeService;
    private readonly IDatabaseService _databaseService;

    // Keywords for meal type matching (checked against CategoryName and TagsJson)
    private static readonly string[] BreakfastKeywords =
        ["breakfast", "brunch", "morning", "pancake", "waffle", "omelette", "omelet"];
    private static readonly string[] LunchDinnerKeywords =
        ["main", "dinner", "lunch", "entree", "entrée", "casserole", "soup", "stew",
         "pasta", "seafood", "meat", "poultry", "chicken", "beef", "pork", "fish",
         "salad", "sandwich", "bowl", "curry", "stir fry", "roast", "grill"];
    private static readonly string[] SnackKeywords =
        ["snack", "appetizer", "dip", "side", "finger food", "small bite"];
    private static readonly string[] DessertKeywords =
        ["dessert", "sweet", "cake", "cookie", "pie", "pudding", "ice cream"];

    // Keywords to check against ingredients for diet type conflicts
    private static readonly Dictionary<DietType, string[]> DietConflictKeywords = new()
    {
        [DietType.Vegan] = ["chicken", "beef", "pork", "lamb", "turkey", "fish", "salmon", "tuna",
            "shrimp", "crab", "lobster", "egg", "milk", "cream", "cheese", "butter", "yogurt",
            "honey", "bacon", "sausage", "ham", "steak", "ground beef", "ground turkey"],
        [DietType.Vegetarian] = ["chicken", "beef", "pork", "lamb", "turkey", "fish", "salmon", "tuna",
            "shrimp", "crab", "lobster", "bacon", "sausage", "ham", "steak", "ground beef", "ground turkey"],
        [DietType.Pescatarian] = ["chicken", "beef", "pork", "lamb", "turkey",
            "bacon", "sausage", "ham", "steak", "ground beef", "ground turkey"],
        [DietType.GlutenFree] = ["flour", "bread", "pasta", "noodle", "cracker", "tortilla",
            "breadcrumb", "panko", "soy sauce", "barley", "wheat"],
        [DietType.DairyFree] = ["milk", "cream", "cheese", "butter", "yogurt", "sour cream",
            "whipped cream", "cream cheese", "mozzarella", "cheddar", "parmesan"],
    };

    public MealPlanRecipeService(
        IRecipeService recipeService,
        ISavedRecipeService savedRecipeService,
        IDatabaseService databaseService)
    {
        _recipeService = recipeService;
        _savedRecipeService = savedRecipeService;
        _databaseService = databaseService;
    }

    public async Task<List<SavedRecipe>> BuildRecipePoolAsync(int userId)
    {
        // The recipe catalog is bundled on-device (LocalRecipeService), so the pool
        // is simply the local recipes that have nutrition — no API fetch/caching.
        //
        // This previously iterated every category, re-fetched each recipe's detail,
        // and re-saved a copy with a 100ms delay between batches. Against the bundled
        // catalog that did nothing useful but took minutes and created thousands of
        // duplicate "User" rows. See ApplyDuplicateRecipeCleanupAsync.
        return await GetRecipePoolAsync(userId);
    }

    public async Task<List<SavedRecipe>> GetRecipePoolAsync(int userId)
    {
        var conn = await _databaseService.GetConnectionAsync();
        // The bundled catalog is shared (stored with UserId=0), so don't filter by
        // user — just take the Wikibooks recipes that have a calorie value.
        return await conn.QueryAsync<SavedRecipe>(
            "SELECT * FROM SavedRecipe WHERE SourceProvider = 'Wikibooks' " +
            "AND CaloriesPerServing IS NOT NULL AND CaloriesPerServing > 0");
    }

    public List<SavedRecipe> FilterForMealType(List<SavedRecipe> pool, MealType mealType)
    {
        var keywords = mealType switch
        {
            MealType.Breakfast => BreakfastKeywords,
            MealType.MorningSnack or MealType.AfternoonSnack or MealType.EveningSnack => SnackKeywords,
            _ => LunchDinnerKeywords // Lunch, Dinner, PreWorkout, PostWorkout
        };

        var matches = pool.Where(r => MatchesKeywords(r, keywords)).ToList();

        // If too few matches for snack slots, also include desserts and breakfast items
        if (matches.Count < 3 && mealType is MealType.MorningSnack or MealType.AfternoonSnack or MealType.EveningSnack)
        {
            var extras = pool.Where(r => MatchesKeywords(r, DessertKeywords) || MatchesKeywords(r, BreakfastKeywords))
                .Where(r => !matches.Contains(r));
            matches.AddRange(extras);
        }

        // If still too few, return all recipes as fallback (better than nothing)
        if (matches.Count < 3)
            return pool;

        return matches;
    }

    public List<SavedRecipe> FilterByDiet(List<SavedRecipe> pool, NutritionProfile profile)
    {
        // Batch-load ALL ingredients in one query instead of N+1 queries
        var ingredientCache = new Dictionary<int, List<string>>();
        var conn = _databaseService.GetConnectionAsync().GetAwaiter().GetResult();
        var allIngredients = conn.Table<SavedRecipeIngredient>().ToListAsync().GetAwaiter().GetResult();

        foreach (var ing in allIngredients)
        {
            if (!ingredientCache.ContainsKey(ing.SavedRecipeId))
                ingredientCache[ing.SavedRecipeId] = new List<string>();
            ingredientCache[ing.SavedRecipeId].Add(ing.Description.ToLowerInvariant());
        }

        var filtered = pool.ToList();

        // Filter by diet type
        if (DietConflictKeywords.TryGetValue(profile.DietType, out var conflicts))
        {
            filtered = filtered.Where(r => !HasIngredientConflict(r, conflicts, ingredientCache)).ToList();
        }

        // Filter by allergies
        if (!string.IsNullOrEmpty(profile.Allergies))
        {
            var allergies = profile.Allergies.Split(',', StringSplitOptions.TrimEntries)
                .Where(a => !string.Equals(a, "None", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (allergies.Length > 0)
            {
                filtered = filtered.Where(r => !HasIngredientConflict(r, allergies, ingredientCache)).ToList();
            }
        }

        // Filter by foods to avoid
        if (!string.IsNullOrEmpty(profile.FoodsToAvoid))
        {
            var avoid = profile.FoodsToAvoid.Split(',', StringSplitOptions.TrimEntries);
            filtered = filtered.Where(r => !HasIngredientConflict(r, avoid, ingredientCache)).ToList();
        }

        return filtered;
    }

    public SavedRecipe? SelectBestMatch(List<SavedRecipe> candidates, double targetCalories, HashSet<int> usedIds,
        double targetProteinPct = 0.30, double targetCarbsPct = 0.40, double targetFatPct = 0.30)
    {
        if (candidates.Count == 0) return null;

        // Score each recipe on calorie proximity AND macro alignment
        var scored = candidates.Select(r =>
        {
            var cal = (double)(r.CaloriesPerServing ?? 1);
            var protein = r.ProteinGrams ?? 0;
            var carbs = r.CarbsGrams ?? 0;
            var fat = r.FatGrams ?? 0;
            var totalMacroG = protein + carbs + fat;

            // Macro ratios of this recipe (by calorie contribution)
            double recipePPct = 0, recipeCPct = 0, recipeFPct = 0;
            if (cal > 0)
            {
                recipePPct = (protein * 4) / cal;
                recipeCPct = (carbs * 4) / cal;
                recipeFPct = (fat * 9) / cal;
            }

            // Macro deviation score (lower = better match to target ratios)
            var macroDeviation =
                Math.Abs(recipePPct - targetProteinPct) +
                Math.Abs(recipeCPct - targetCarbsPct) +
                Math.Abs(recipeFPct - targetFatPct);

            // Calorie deviation (normalized to 0-1 range)
            var calDeviation = Math.Abs(cal - targetCalories) / Math.Max(targetCalories, 1);

            // Combined score: 60% macro match, 40% calorie match
            // Lower is better
            var score = macroDeviation * 0.6 + calDeviation * 0.4;

            // Penalty for already-used recipes
            if (usedIds.Contains(r.Id))
                score += 0.5;

            return (recipe: r, score);
        })
        .OrderBy(x => x.score)
        .ToList();

        // Pick randomly from top 5 best matches for variety
        var topCount = Math.Min(5, scored.Count);
        var rng = new Random();
        return scored[rng.Next(topCount)].recipe;
    }

    private static bool MatchesKeywords(SavedRecipe recipe, string[] keywords)
    {
        // Check category name
        if (!string.IsNullOrEmpty(recipe.CategoryName) &&
            keywords.Any(k => recipe.CategoryName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Check tags
        if (!string.IsNullOrEmpty(recipe.TagsJson))
        {
            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(recipe.TagsJson);
                if (tags != null && tags.Any(t => keywords.Any(k =>
                    t.Contains(k, StringComparison.OrdinalIgnoreCase))))
                    return true;
            }
            catch { /* ignore deserialization errors */ }
        }

        // Check recipe name
        if (keywords.Any(k => recipe.RecipeName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static bool HasIngredientConflict(SavedRecipe recipe, string[] conflictWords,
        Dictionary<int, List<string>> ingredientCache)
    {
        // Check recipe name
        if (conflictWords.Any(c =>
            recipe.RecipeName.Contains(c, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Check actual ingredients
        if (ingredientCache.TryGetValue(recipe.Id, out var ingredients))
        {
            foreach (var ingredient in ingredients)
            {
                if (conflictWords.Any(c => ingredient.Contains(c, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }

        return false;
    }
}

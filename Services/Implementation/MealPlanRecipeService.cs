using System.Text.Json;
using IntelligentPersonalHealthOptimization.Data;
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

    // Diet-type conflicts are NO LONGER decided by runtime keyword matching — every
    // catalog recipe carries precomputed IsVegan/IsVegetarian/IsPescatarian/
    // IsGlutenFree/IsDairyFree flags (classified offline from the full ingredient
    // list, conservative on ambiguous ingredients). FilterByDietAsync reads those.
    // Allergies / foods-to-avoid are still ingredient-scanned (free-text, per-user).

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
        var pool = await conn.QueryAsync<SavedRecipe>(
            "SELECT * FROM SavedRecipe WHERE SourceProvider = 'Wikibooks' " +
            "AND CaloriesPerServing IS NOT NULL AND CaloriesPerServing > 0");
        // Drinks (beverages/cocktails/juice/wine) and pure components (sauces, spice
        // mixes, dressings, syrups…) are not meals — keep them in the catalog but out
        // of meal plans. Smoothies/shakes are kept (see RecipeCategoryGroups).
        // Also keep "Indulgent"-tier dishes (rich/fatty/salty) out of generated plans
        // — this is a fitness app. Healthy + Moderate + healthy treats stay in. Null
        // tier (not yet classified) is treated as eligible, never wrongly excluded.
        return pool
            .Where(r => RecipeCategoryGroups.IsMealPlanEligible(r.CategoryName))
            .Where(r => r.HealthTier != RecipeHealth.Indulgent)
            .ToList();
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

    public async Task<List<SavedRecipe>> FilterByDietAsync(List<SavedRecipe> pool, NutritionProfile profile)
    {
        // Diet type: filter by the precomputed per-recipe flag (authoritative,
        // classified offline from the full ingredient list). Keto/Paleo/Mediterranean/
        // Standard/Halal/Kosher have no recipe-level flag — macros handle those.
        var filtered = profile.DietType switch
        {
            DietType.Vegan => pool.Where(r => r.IsVegan).ToList(),
            DietType.Vegetarian => pool.Where(r => r.IsVegetarian).ToList(),
            DietType.Pescatarian => pool.Where(r => r.IsPescatarian).ToList(),
            DietType.GlutenFree => pool.Where(r => r.IsGlutenFree).ToList(),
            DietType.DairyFree => pool.Where(r => r.IsDairyFree).ToList(),
            DietType.Keto => pool.Where(r => r.IsKeto).ToList(),
            DietType.Paleo => pool.Where(r => r.IsPaleo).ToList(),
            DietType.Halal => pool.Where(r => r.IsHalal).ToList(),
            DietType.Kosher => pool.Where(r => r.IsKosher).ToList(),
            DietType.Mediterranean => pool.Where(r => r.IsMediterranean).ToList(),
            _ => pool.ToList(), // Standard: no recipe-level exclusions
        };

        // Allergies + foods-to-avoid are free-text per-user, so still need an
        // ingredient scan — but only load ingredients when one is actually set.
        var hasAllergies = !string.IsNullOrEmpty(profile.Allergies);
        var hasAvoid = !string.IsNullOrEmpty(profile.FoodsToAvoid);
        if (!hasAllergies && !hasAvoid)
            return filtered;

        var conn = await _databaseService.GetConnectionAsync();
        var allIngredients = await conn.Table<SavedRecipeIngredient>().ToListAsync();
        var ingredientCache = new Dictionary<int, List<string>>();
        foreach (var ing in allIngredients)
        {
            if (!ingredientCache.TryGetValue(ing.SavedRecipeId, out var list))
            {
                list = new List<string>();
                ingredientCache[ing.SavedRecipeId] = list;
            }
            list.Add(ing.Description.ToLowerInvariant());
        }

        if (hasAllergies)
        {
            var allergies = profile.Allergies.Split(',', StringSplitOptions.TrimEntries)
                .Where(a => !string.Equals(a, "None", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (allergies.Length > 0)
                filtered = filtered.Where(r => !HasIngredientConflict(r, allergies, ingredientCache)).ToList();
        }

        if (hasAvoid)
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
        return scored[Random.Shared.Next(topCount)].recipe;
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

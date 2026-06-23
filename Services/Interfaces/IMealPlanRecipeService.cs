using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IMealPlanRecipeService
{
    Task<List<SavedRecipe>> BuildRecipePoolAsync(int userId);
    Task<List<SavedRecipe>> GetRecipePoolAsync(int userId);
    List<SavedRecipe> FilterForMealType(List<SavedRecipe> pool, MealType mealType);
    Task<List<SavedRecipe>> FilterByDietAsync(List<SavedRecipe> pool, NutritionProfile profile);
    SavedRecipe? SelectBestMatch(List<SavedRecipe> candidates, double targetCalories, HashSet<int> usedIds,
        double targetProteinPct = 0.30, double targetCarbsPct = 0.40, double targetFatPct = 0.30);
}

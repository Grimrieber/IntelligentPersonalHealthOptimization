using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IFoodService
{
    Task<List<Food>> SearchFoodsAsync(string query, int limit = 20);
    Task<Food?> GetFoodByBarcodeAsync(string barcode);
    Task<Food?> GetFoodByIdAsync(int id);
    Task<List<Food>> GetFoodsByCategoryAsync(FoodCategory category);
    Task<Food> AddCustomFoodAsync(Food food);
    Task<FoodLogEntry> LogFoodAsync(int userId, int foodId, MealType mealType, double servingSizeG, DateTime logDate);
    Task<List<FoodLogEntry>> GetFoodLogAsync(int userId, DateTime date);

    /// <summary>Distinct foods this user has logged most recently (excludes
    /// recipe-based log entries) — for one-tap re-add on the Add Food page.</summary>
    Task<List<Food>> GetRecentlyLoggedFoodsAsync(int userId, int take = 8);
    Task<(double calories, double proteinG, double carbsG, double fatG)> GetDailyTotalsAsync(int userId, DateTime date);
    Task<(double avgCalories, double avgProteinG, double avgCarbsG, double avgFatG)> GetWeeklyAveragesAsync(int userId);
    Task DeleteFoodLogEntryAsync(int id);

    /// <summary>Change a logged entry's serving size (grams), rescaling its stored
    /// macros proportionally. No-op for entries with no real serving (recipe logs).</summary>
    Task UpdateFoodLogServingAsync(int id, double newServingG);

    /// <summary>Copy every food-log entry from one day to another (for "log the same
    /// as yesterday"). Returns how many entries were copied.</summary>
    Task<int> CopyLogDayAsync(int userId, DateTime fromDate, DateTime toDate);
}

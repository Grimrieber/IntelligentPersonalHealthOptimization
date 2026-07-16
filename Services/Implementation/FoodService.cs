using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class FoodService : IFoodService
{
    private readonly IDatabaseService _databaseService;

    public FoodService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<Food>> SearchFoodsAsync(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Food>();

        var db = await _databaseService.GetConnectionAsync();
        // Filter in SQL with LIKE + COLLATE NOCASE rather than loading every food
        // and filtering in memory. (Raw SQL because sqlite-net's LINQ translation
        // of .Contains() in a compound Where is unreliable — same reason as
        // LocalRecipeService.SearchRecipesAsync.)
        var like = "%" + query.Trim() + "%";
        return await db.QueryAsync<Food>(
            "SELECT * FROM Food WHERE IsActive = 1 AND " +
            "(Name LIKE ? COLLATE NOCASE OR (Brand IS NOT NULL AND Brand LIKE ? COLLATE NOCASE)) " +
            "ORDER BY Name LIMIT ?",
            like, like, limit);
    }

    public async Task<Food?> GetFoodByBarcodeAsync(string barcode)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<Food>()
            .Where(f => f.Barcode == barcode && f.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<Food?> GetFoodByIdAsync(int id)
    {
        return await _databaseService.GetByIdAsync<Food>(id);
    }

    public async Task<List<Food>> GetFoodsByCategoryAsync(FoodCategory category)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<Food>()
            .Where(f => f.FoodCategory == category && f.IsActive)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<Food> AddCustomFoodAsync(Food food)
    {
        food.IsUserCreated = true;
        food.IsActive = true;
        await _databaseService.InsertAsync(food);
        return food;
    }

    public async Task<FoodLogEntry> LogFoodAsync(int userId, int foodId, MealType mealType, double servingSizeG, DateTime logDate)
    {
        var food = await _databaseService.GetByIdAsync<Food>(foodId);
        if (food == null) throw new InvalidOperationException("Food not found");

        var factor = servingSizeG / 100.0;
        var entry = new FoodLogEntry
        {
            UserId = userId,
            FoodId = foodId,
            LogDate = logDate.Date,
            MealType = mealType,
            ServingSizeG = servingSizeG,
            Calories = Math.Round(food.CaloriesPer100g * factor, 1),
            ProteinG = Math.Round(food.ProteinPer100g * factor, 1),
            CarbsG = Math.Round(food.CarbsPer100g * factor, 1),
            FatG = Math.Round(food.FatPer100g * factor, 1),
            FiberG = Math.Round(food.FiberPer100g * factor, 1)
        };

        await _databaseService.InsertAsync(entry);
        return entry;
    }

    public async Task<List<FoodLogEntry>> GetFoodLogAsync(int userId, DateTime date)
    {
        var db = await _databaseService.GetConnectionAsync();
        var dateStart = date.Date;
        var dateEnd = dateStart.AddDays(1);
        return await db.Table<FoodLogEntry>()
            .Where(e => e.UserId == userId && e.LogDate >= dateStart && e.LogDate < dateEnd)
            .OrderBy(e => e.MealType)
            .ToListAsync();
    }

    public async Task<List<Food>> GetRecentlyLoggedFoodsAsync(int userId, int take = 8)
    {
        var db = await _databaseService.GetConnectionAsync();
        // Most-recent-first log rows for real foods (FoodId > 0 excludes recipe-based
        // entries, which carry SavedRecipeId instead). Dedupe by FoodId, resolve to Food.
        var rows = await db.QueryAsync<FoodLogEntry>(
            "SELECT * FROM FoodLogEntry WHERE UserId = ? AND FoodId > 0 ORDER BY CreatedAt DESC LIMIT 200",
            userId);

        var seen = new HashSet<int>();
        var result = new List<Food>();
        foreach (var e in rows)
        {
            if (!seen.Add(e.FoodId)) continue;
            var food = await _databaseService.GetByIdAsync<Food>(e.FoodId);
            if (food != null && food.IsActive) result.Add(food);
            if (result.Count >= take) break;
        }
        return result;
    }

    public async Task<int> CopyLogDayAsync(int userId, DateTime fromDate, DateTime toDate)
    {
        var entries = await GetFoodLogAsync(userId, fromDate);
        foreach (var e in entries)
        {
            await _databaseService.InsertAsync(new FoodLogEntry
            {
                UserId = userId,
                FoodId = e.FoodId,
                SavedRecipeId = e.SavedRecipeId,
                LogDate = toDate.Date,
                MealType = e.MealType,
                ServingSizeG = e.ServingSizeG,
                Calories = e.Calories,
                ProteinG = e.ProteinG,
                CarbsG = e.CarbsG,
                FatG = e.FatG,
                FiberG = e.FiberG,
                Notes = e.Notes,
            });
        }
        return entries.Count;
    }

    public async Task<(double calories, double proteinG, double carbsG, double fatG)> GetDailyTotalsAsync(int userId, DateTime date)
    {
        var entries = await GetFoodLogAsync(userId, date);
        return (
            entries.Sum(e => e.Calories),
            entries.Sum(e => e.ProteinG),
            entries.Sum(e => e.CarbsG),
            entries.Sum(e => e.FatG)
        );
    }

    public async Task<(double avgCalories, double avgProteinG, double avgCarbsG, double avgFatG)> GetWeeklyAveragesAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        var weekAgo = DateTime.UtcNow.Date.AddDays(-7);
        var entries = await db.Table<FoodLogEntry>()
            .Where(e => e.UserId == userId && e.LogDate >= weekAgo)
            .ToListAsync();

        if (entries.Count == 0) return (0, 0, 0, 0);

        var days = entries.Select(e => e.LogDate.Date).Distinct().Count();
        if (days == 0) days = 1;

        return (
            entries.Sum(e => e.Calories) / days,
            entries.Sum(e => e.ProteinG) / days,
            entries.Sum(e => e.CarbsG) / days,
            entries.Sum(e => e.FatG) / days
        );
    }

    public async Task DeleteFoodLogEntryAsync(int id)
    {
        var entry = await _databaseService.GetByIdAsync<FoodLogEntry>(id);
        if (entry != null)
            await _databaseService.DeleteAsync(entry);
    }

    public async Task UpdateFoodLogServingAsync(int id, double newServingG)
    {
        if (newServingG <= 0) return;
        var entry = await _databaseService.GetByIdAsync<FoodLogEntry>(id);
        if (entry == null || entry.ServingSizeG <= 0) return;

        // Scale the stored macros by the serving change — works for any entry that
        // has a real serving (LogFoodAsync stored macros ∝ serving), and avoids
        // re-fetching the source food.
        var factor = newServingG / entry.ServingSizeG;
        entry.Calories = Math.Round(entry.Calories * factor, 1);
        entry.ProteinG = Math.Round(entry.ProteinG * factor, 1);
        entry.CarbsG = Math.Round(entry.CarbsG * factor, 1);
        entry.FatG = Math.Round(entry.FatG * factor, 1);
        entry.ServingSizeG = newServingG;
        await _databaseService.UpdateAsync(entry);
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using IntelligentPersonalHealthOptimization.Data;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class MealSelectionViewModel : BaseViewModel, IQueryAttributable
{
    private readonly INutritionService _nutritionService;
    private readonly ISavedRecipeService _savedRecipeService;
    private readonly IUserService _userService;
    private readonly IDatabaseService _databaseService;
    private readonly IMealPlanRecipeService _mealPlanRecipeService;

    public MealSelectionViewModel(INutritionService nutritionService,
        ISavedRecipeService savedRecipeService, IUserService userService,
        IDatabaseService databaseService, IMealPlanRecipeService mealPlanRecipeService)
    {
        _nutritionService = nutritionService;
        _savedRecipeService = savedRecipeService;
        _userService = userService;
        _databaseService = databaseService;
        _mealPlanRecipeService = mealPlanRecipeService;
        Title = "Choose a Meal";
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<MealOptionItem> _filteredMeals = new();

    [ObservableProperty]
    private string _mealTypeLabel = string.Empty;

    [ObservableProperty]
    private string _targetCaloriesLabel = string.Empty;

    private MealType _mealType;
    private DietType _dietType;
    private int _mealPlanDayId;
    private int _nutritionProfileId;
    private int _targetCaloriesForSlot;
    private List<MealOptionItem> _allMeals = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("mealType", out var mt))
            _mealType = (MealType)Convert.ToInt32(mt);
        if (query.TryGetValue("dietType", out var dt))
            _dietType = (DietType)Convert.ToInt32(dt);
        if (query.TryGetValue("mealPlanDayId", out var dayId))
            _mealPlanDayId = Convert.ToInt32(dayId);
        if (query.TryGetValue("nutritionProfileId", out var profId))
            _nutritionProfileId = Convert.ToInt32(profId);
        if (query.TryGetValue("targetCalories", out var tc))
            _targetCaloriesForSlot = Convert.ToInt32(tc);

        MealTypeLabel = FormatMealType(_mealType);
        TargetCaloriesLabel = $"Target: {_targetCaloriesForSlot} kcal";
        _ = LoadMealsAsync();
    }

    private async Task LoadMealsAsync()
    {
        IsBusy = true;
        try
        {
            // Load from the bundled recipe catalog (shared, stored with UserId=0)
            // — every recipe with nutrition data. Previously used
            // GetSavedRecipesAsync(user.Id), which only returns per-user rows and
            // is empty for the bundled catalog, so the swap list showed nothing.
            var db = await _databaseService.GetConnectionAsync();
            var allWithNutrition = await db.QueryAsync<SavedRecipe>(
                "SELECT * FROM SavedRecipe WHERE SourceProvider = 'Wikibooks' " +
                "AND CaloriesPerServing IS NOT NULL AND CaloriesPerServing > 0");
            // Exclude drinks and pure components (sauces/spice mixes/etc.) — they aren't
            // meals, so they shouldn't appear as swap options either. Smoothies/shakes kept.
            var withNutrition = allWithNutrition
                .Where(r => RecipeCategoryGroups.IsMealPlanEligible(r.CategoryName)).ToList();

            // Restrict to recipes appropriate for this meal type (breakfast/lunch/
            // dinner/snack) so e.g. a drink doesn't show up as a lunch swap. Falls
            // back to the full list when too few match (see FilterForMealType).
            var candidates = _mealPlanRecipeService.FilterForMealType(withNutrition, _mealType);

            // Also respect the user's diet type, allergies, and foods-to-avoid so a
            // manual swap can't pick a recipe that conflicts with their diet (the
            // auto-generated plan already does this). Fall back to the unfiltered
            // meal-type list if diet filtering would leave nothing to pick.
            var user = await _userService.GetCurrentUserAsync();
            var profile = user != null ? await _nutritionService.GetNutritionProfileAsync(user.Id) : null;
            if (profile != null)
            {
                var dietFiltered = await _mealPlanRecipeService.FilterByDietAsync(candidates, profile);
                if (dietFiltered.Count > 0)
                    candidates = dietFiltered;
            }

            // Add a protein target to the header — this app's users are goal-driven,
            // so "which similar-calorie meal" usually comes down to protein. Prorate
            // the daily protein goal to this slot's share of daily calories.
            if (profile != null && profile.TargetCalories > 0 && _targetCaloriesForSlot > 0)
            {
                var slotProtein = profile.TargetProteinG * (_targetCaloriesForSlot / (double)profile.TargetCalories);
                TargetCaloriesLabel = $"Target: ~{_targetCaloriesForSlot} kcal · ~{slotProtein:F0}g protein";
            }

            _allMeals = candidates.Select(r =>
            {
                var recipeCal = r.CaloriesPerServing ?? 1;
                var servings = Math.Round((_targetCaloriesForSlot / (double)recipeCal) * 2) / 2.0;
                servings = Math.Max(0.5, Math.Min(servings, 5));
                var totalCal = (int)(recipeCal * servings);

                string servingsNote;
                if (servings > 1.01)
                {
                    var text = servings % 1 == 0 ? $"{servings:F0}" : $"{servings:F1}";
                    servingsNote = $"Make {text}x this recipe";
                }
                else if (servings < 0.99)
                {
                    servingsNote = $"Have {servings:F1} of a serving";
                }
                else
                {
                    servingsNote = "1 serving";
                }

                var delta = Math.Abs(totalCal - _targetCaloriesForSlot);
                return new MealOptionItem
                {
                    SavedRecipeId = r.Id,
                    Name = r.RecipeName,
                    Category = r.CategoryName,
                    CaloriesPerServing = recipeCal,
                    Servings = servings,
                    TotalCalories = totalCal,
                    EstimatedCalories = $"{totalCal} kcal",
                    ServingsNote = servingsNote,
                    MacroSummary = $"P: {(r.ProteinGrams ?? 0) * servings:F0}g  " +
                                   $"C: {(r.CarbsGrams ?? 0) * servings:F0}g  " +
                                   $"F: {(r.FatGrams ?? 0) * servings:F0}g",
                    ProteinG = (r.ProteinGrams ?? 0) * servings,
                    CarbsG = (r.CarbsGrams ?? 0) * servings,
                    FatG = (r.FatGrams ?? 0) * servings,
                    CalorieDelta = delta,
                    MatchText = delta == 0 ? "exact match" : $"±{delta} kcal",
                    HealthTier = r.HealthTier,
                    IsHealthyTreat = r.IsHealthyTreat,
                };
            })
            .OrderBy(m => m.CalorieDelta)
            .ToList();

            // The list is sorted best-first — flag the closest so the smart ordering is visible.
            if (_allMeals.Count > 0)
                _allMeals[0].IsBestMatch = true;

            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? _allMeals
            : _allMeals.Where(m =>
                m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        FilteredMeals = new ObservableCollection<MealOptionItem>(filtered);
    }

    [RelayCommand]
    private async Task SelectMealAsync(MealOptionItem item)
    {
        if (item == null) return;

        // Create the meal plan item directly with recipe data
        var db = await _databaseService.GetConnectionAsync();

        // Delete existing non-shake items for this meal type on this day
        var existingItems = await db.Table<MealPlanItem>()
            .Where(i => i.MealPlanDayId == _mealPlanDayId)
            .ToListAsync();
        foreach (var existing in existingItems.Where(i =>
            i.MealType == _mealType && i.MealName != "Protein Shake"))
        {
            await db.DeleteAsync(existing);
        }

        // Insert new recipe-based item
        var newItem = new MealPlanItem
        {
            MealPlanDayId = _mealPlanDayId,
            SavedRecipeId = item.SavedRecipeId,
            MealType = _mealType,
            MealName = item.Name,
            Servings = item.Servings,
            Calories = item.TotalCalories,
            ProteinG = item.ProteinG,
            CarbsG = item.CarbsG,
            FatG = item.FatG,
            OrderIndex = 0
        };
        await db.InsertAsync(newItem);

        // Recalculate day total
        var dayItems = await db.Table<MealPlanItem>()
            .Where(i => i.MealPlanDayId == _mealPlanDayId)
            .ToListAsync();
        var day = await db.FindAsync<MealPlanDay>(_mealPlanDayId);
        if (day != null)
        {
            day.TotalCalories = (int)dayItems.Sum(i => i.Calories);
            await db.UpdateAsync(day);
        }

        // Notify meal plan page to refresh
        WeakReferenceMessenger.Default.Send(new MealSelectedMessage(
            item.Name, _mealType, _mealPlanDayId, _nutritionProfileId));

        await Shell.Current.GoToAsync("..");
    }

    private static string FormatMealType(MealType mealType) => mealType switch
    {
        MealType.Breakfast => "Breakfast",
        MealType.MorningSnack => "Morning Snack",
        MealType.Lunch => "Lunch",
        MealType.AfternoonSnack => "Afternoon Snack",
        MealType.Dinner => "Dinner",
        MealType.EveningSnack => "Evening Snack",
        MealType.PreWorkout => "Pre-Workout",
        MealType.PostWorkout => "Post-Workout",
        _ => mealType.ToString()
    };
}

public class MealOptionItem
{
    public int SavedRecipeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CaloriesPerServing { get; set; }
    public double Servings { get; set; }
    public int TotalCalories { get; set; }
    public string EstimatedCalories { get; set; } = string.Empty;
    public string ServingsNote { get; set; } = string.Empty;
    public string MacroSummary { get; set; } = string.Empty;
    public double ProteinG { get; set; }
    public double CarbsG { get; set; }
    public double FatG { get; set; }
    public int CalorieDelta { get; set; }
    public string MatchText { get; set; } = string.Empty;
    public bool IsBestMatch { get; set; }

    // Health-tier badge — same signal the recipe/meal cards show, so a healthier
    // swap is obvious at a glance (shared HealthBadgeStyle keeps it consistent).
    public string? HealthTier { get; set; }
    public bool IsHealthyTreat { get; set; }
    public bool ShowHealthBadge => Data.HealthBadgeStyle.ShouldShow(HealthTier);
    public string HealthBadgeText => Data.HealthBadgeStyle.TextFor(HealthTier, IsHealthyTreat);
    public Microsoft.Maui.Graphics.Color HealthBadgeColor => Data.HealthBadgeStyle.ColorFor(HealthTier);
}

public record MealSelectedMessage(string MealName, MealType MealType, int MealPlanDayId, int NutritionProfileId);

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class FoodLogViewModel : BaseViewModel
{
    private readonly IFoodService _foodService;
    private readonly INutritionService _nutritionService;
    private readonly IUserService _userService;

    public FoodLogViewModel(IFoodService foodService, INutritionService nutritionService,
        IUserService userService)
    {
        _foodService = foodService;
        _nutritionService = nutritionService;
        _userService = userService;
        Title = "Food Log";
    }

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private string _dateDisplayText = DateTime.Today.ToString("dddd, MMMM dd");

    // Nutrition targets
    [ObservableProperty]
    private bool _hasNutritionTargets;

    [ObservableProperty]
    private string _targetCaloriesText = string.Empty;

    [ObservableProperty]
    private string _consumedCaloriesText = "0";

    [ObservableProperty]
    private string _remainingCaloriesText = string.Empty;

    [ObservableProperty]
    private bool _isCaloriesOver;

    [ObservableProperty]
    private string _proteinProgress = string.Empty;

    [ObservableProperty]
    private string _carbsProgress = string.Empty;

    [ObservableProperty]
    private string _fatProgress = string.Empty;

    [ObservableProperty]
    private bool _isProteinOver;

    [ObservableProperty]
    private bool _isCarbsOver;

    [ObservableProperty]
    private bool _isFatOver;

    // 0–1 fill for the macro progress bars.
    [ObservableProperty]
    private double _proteinBar;

    [ObservableProperty]
    private double _carbsBar;

    [ObservableProperty]
    private double _fatBar;

    // Meal groups
    [ObservableProperty]
    private ObservableCollection<FoodLogMealGroup> _mealGroups = new();

    [ObservableProperty]
    private bool _hasEntries;

    private int _userId;
    private NutritionProfile? _profile;

    partial void OnSelectedDateChanged(DateTime value)
    {
        DateDisplayText = value.ToString("dddd, MMMM dd");
        LoadDataCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;
            _userId = user.Id;

            // Load nutrition targets
            _profile = await _nutritionService.GetNutritionProfileAsync(user.Id);
            HasNutritionTargets = _profile != null && _profile.TargetCalories > 0;

            // Load today's meal plan items
            var todayPlanItems = new List<MealPlanItem>();
            if (_profile != null)
            {
                var activePlan = await _nutritionService.GetActiveMealPlanAsync(user.Id);
                if (activePlan != null)
                {
                    var allDays = await _nutritionService.GetMealPlanDaysAsync(activePlan.Id);
                    var dayName = SelectedDate.DayOfWeek.ToString();
                    var todayDay = allDays.FirstOrDefault(d =>
                        string.Equals(d.DayName, dayName, StringComparison.OrdinalIgnoreCase));
                    if (todayDay != null)
                    {
                        todayPlanItems = await _nutritionService.GetMealItemsAsync(todayDay.Id);
                    }
                }
            }

            // Load food log entries
            var dailyTotals = await _foodService.GetDailyTotalsAsync(user.Id, SelectedDate);
            var entries = await _foodService.GetFoodLogAsync(user.Id, SelectedDate);

            // Update nutrition progress
            UpdateNutritionProgress(dailyTotals);

            // Collect active meal types (from plan + logged entries)
            var activeMealTypes = new HashSet<MealType>();
            foreach (var item in todayPlanItems)
                activeMealTypes.Add(item.MealType);
            foreach (var entry in entries)
                activeMealTypes.Add(entry.MealType);

            // Build groups for only active meal types
            var groups = new ObservableCollection<FoodLogMealGroup>();

            foreach (var mealType in activeMealTypes.OrderBy(mt => mt))
            {
                var mealEntries = entries.Where(e => e.MealType == mealType).ToList();
                var plannedItems = todayPlanItems.Where(i => i.MealType == mealType)
                    .OrderBy(i => i.OrderIndex).ToList();

                // Build logged entry display items
                var loggedItems = new ObservableCollection<FoodLogEntryDisplayItem>();
                foreach (var entry in mealEntries)
                {
                    var food = await _foodService.GetFoodByIdAsync(entry.FoodId);
                    // Recipe-logged entries have FoodId 0 and store the recipe name in
                    // Notes — use that so they don't render as "Food #0".
                    var name = food?.Name;
                    if (string.IsNullOrWhiteSpace(name))
                        name = !string.IsNullOrWhiteSpace(entry.Notes) ? entry.Notes : $"Food #{entry.FoodId}";
                    loggedItems.Add(new FoodLogEntryDisplayItem
                    {
                        Id = entry.Id,
                        FoodName = name,
                        ServingSize = $"{entry.ServingSizeG:F0}g",
                        ServingSizeG = entry.ServingSizeG,
                        Calories = $"{entry.Calories:F0} kcal",
                        ProteinG = $"{entry.ProteinG:F0}g",
                        CarbsG = $"{entry.CarbsG:F0}g",
                        FatG = $"{entry.FatG:F0}g"
                    });
                }

                // Build planned meal display items
                var plannedDisplayItems = new ObservableCollection<FoodLogPlannedItem>();
                var plannedMealName = string.Empty;
                foreach (var planItem in plannedItems)
                {
                    var food = await _foodService.GetFoodByIdAsync(planItem.FoodId);
                    plannedDisplayItems.Add(new FoodLogPlannedItem
                    {
                        FoodId = planItem.FoodId,
                        FoodName = food?.Name ?? $"Food #{planItem.FoodId}",
                        ServingSize = $"{planItem.ServingSizeG:F0}g",
                        Calories = $"{planItem.Calories:F0} kcal",
                        MacroSummary = $"P:{planItem.ProteinG:F0}g  C:{planItem.CarbsG:F0}g  F:{planItem.FatG:F0}g",
                        ServingSizeG = planItem.ServingSizeG
                    });
                    if (string.IsNullOrEmpty(plannedMealName) && !string.IsNullOrEmpty(planItem.MealName))
                        plannedMealName = planItem.MealName;
                }

                var hasPlanned = plannedDisplayItems.Count > 0;
                var plannedCals = plannedItems.Sum(i => i.Calories);

                groups.Add(new FoodLogMealGroup
                {
                    MealType = mealType,
                    MealTypeName = FormatMealType(mealType),
                    TotalCalories = loggedItems.Count > 0
                        ? $"{mealEntries.Sum(e => e.Calories):F0} kcal"
                        : string.Empty,
                    Items = loggedItems,
                    HasItems = loggedItems.Count > 0,
                    HasPlannedMeal = hasPlanned,
                    PlannedMealName = plannedMealName,
                    PlannedItems = plannedDisplayItems,
                    PlannedCalories = hasPlanned ? $"{plannedCals:F0} kcal" : string.Empty,
                    ShowPlannedMeal = hasPlanned && loggedItems.Count == 0
                });
            }

            MealGroups = groups;
            HasEntries = entries.Count > 0 || todayPlanItems.Count > 0;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Food log load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateNutritionProgress(
        (double calories, double proteinG, double carbsG, double fatG) totals)
    {
        if (!HasNutritionTargets || _profile == null)
        {
            TargetCaloriesText = string.Empty;
            ConsumedCaloriesText = "0";
            RemainingCaloriesText = string.Empty;
            ProteinProgress = string.Empty;
            CarbsProgress = string.Empty;
            FatProgress = string.Empty;
            ProteinBar = 0;
            CarbsBar = 0;
            FatBar = 0;
            return;
        }

        var consumed = totals.calories;
        var target = _profile.TargetCalories;
        var remaining = target - consumed;

        TargetCaloriesText = $"{target}";
        ConsumedCaloriesText = $"{consumed:F0}";
        IsCaloriesOver = remaining < 0;
        RemainingCaloriesText = remaining >= 0
            ? $"{remaining:F0} left"
            : $"{Math.Abs(remaining):F0} over";

        IsProteinOver = totals.proteinG > _profile.TargetProteinG;
        ProteinProgress = $"{totals.proteinG:F0} / {_profile.TargetProteinG}g";
        ProteinBar = _profile.TargetProteinG > 0 ? Math.Min(totals.proteinG / _profile.TargetProteinG, 1.0) : 0;

        IsCarbsOver = totals.carbsG > _profile.TargetCarbsG;
        CarbsProgress = $"{totals.carbsG:F0} / {_profile.TargetCarbsG}g";
        CarbsBar = _profile.TargetCarbsG > 0 ? Math.Min(totals.carbsG / _profile.TargetCarbsG, 1.0) : 0;

        IsFatOver = totals.fatG > _profile.TargetFatG;
        FatProgress = $"{totals.fatG:F0} / {_profile.TargetFatG}g";
        FatBar = _profile.TargetFatG > 0 ? Math.Min(totals.fatG / _profile.TargetFatG, 1.0) : 0;
    }

    [RelayCommand]
    private async Task LogPlannedMealAsync(FoodLogMealGroup group)
    {
        if (group == null || !group.HasPlannedMeal) return;

        try
        {
            IsBusy = true;
            foreach (var item in group.PlannedItems)
            {
                await _foodService.LogFoodAsync(
                    _userId, item.FoodId, group.MealType, item.ServingSizeG, SelectedDate);
            }
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Log planned meal", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddMealTypeAsync()
    {
        var allTypes = new[]
        {
            MealType.Breakfast, MealType.MorningSnack, MealType.Lunch,
            MealType.AfternoonSnack, MealType.Dinner, MealType.EveningSnack,
            MealType.PreWorkout, MealType.PostWorkout
        };

        var existingTypes = MealGroups.Select(g => g.MealType).ToHashSet();
        var available = allTypes.Where(t => !existingTypes.Contains(t)).ToArray();

        if (available.Length == 0)
        {
            await Shell.Current.DisplayAlert("All Meals Added",
                "All meal types are already showing.", "OK");
            return;
        }

        var options = available.Select(FormatMealType).ToArray();
        var result = await Shell.Current.DisplayActionSheet("Add Meal", "Cancel", null, options);

        if (result == null || result == "Cancel") return;

        var selectedIndex = Array.IndexOf(options, result);
        if (selectedIndex < 0) return;

        var selectedType = available[selectedIndex];
        await Shell.Current.GoToAsync($"{RouteConstants.AddFoodEntry}?mealType={selectedType}");
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(FoodLogEntryDisplayItem item)
    {
        if (item == null) return;

        bool confirmed = await Shell.Current.DisplayAlert(
            "Delete Entry",
            $"Remove {item.FoodName} from your log?",
            "Delete", "Cancel");

        if (!confirmed) return;

        try
        {
            await _foodService.DeleteFoodLogEntryAsync(item.Id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Delete entry", ex);
        }
    }

    [RelayCommand]
    private async Task EditEntryAsync(FoodLogEntryDisplayItem item)
    {
        if (item == null) return;
        if (!item.CanEditServing)
        {
            await Shell.Current.DisplayAlert("Can't edit",
                "This entry was logged from a recipe. Unlog it and log again to change the amount.", "OK");
            return;
        }

        var input = await Shell.Current.DisplayPromptAsync(
            "Edit serving",
            $"New serving size for {item.FoodName} (grams):",
            "Save", "Cancel",
            initialValue: item.ServingSizeG.ToString("F0"),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(input)) return;
        if (!double.TryParse(input.Trim(), out var grams) || grams <= 0)
        {
            await Shell.Current.DisplayAlert("Invalid amount", "Enter a serving size greater than 0.", "OK");
            return;
        }

        try
        {
            await _foodService.UpdateFoodLogServingAsync(item.Id, grams);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Edit entry serving", ex);
        }
    }

    [RelayCommand]
    private async Task AddFoodToMealAsync(MealType mealType)
    {
        await Shell.Current.GoToAsync($"{RouteConstants.AddFoodEntry}?mealType={mealType}");
    }

    [RelayCommand]
    private void PreviousDay()
    {
        SelectedDate = SelectedDate.AddDays(-1);
    }

    [RelayCommand]
    private void NextDay()
    {
        if (SelectedDate < DateTime.Today)
        {
            SelectedDate = SelectedDate.AddDays(1);
        }
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

public class FoodLogMealGroup
{
    public MealType MealType { get; set; }
    public string MealTypeName { get; set; } = string.Empty;
    public string TotalCalories { get; set; } = string.Empty;
    public ObservableCollection<FoodLogEntryDisplayItem> Items { get; set; } = new();
    public bool HasItems { get; set; }

    // Planned meal integration
    public bool HasPlannedMeal { get; set; }
    public string PlannedMealName { get; set; } = string.Empty;
    public ObservableCollection<FoodLogPlannedItem> PlannedItems { get; set; } = new();
    public string PlannedCalories { get; set; } = string.Empty;
    public bool ShowPlannedMeal { get; set; }
}

public class FoodLogEntryDisplayItem
{
    public int Id { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string ServingSize { get; set; } = string.Empty;
    /// <summary>Numeric serving in grams (0 for recipe-logged entries) — drives inline edit.</summary>
    public double ServingSizeG { get; set; }
    public bool CanEditServing => ServingSizeG > 0;
    public string Calories { get; set; } = string.Empty;
    public string ProteinG { get; set; } = string.Empty;
    public string CarbsG { get; set; } = string.Empty;
    public string FatG { get; set; } = string.Empty;
}

public class FoodLogPlannedItem
{
    public int FoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string ServingSize { get; set; } = string.Empty;
    public string Calories { get; set; } = string.Empty;
    public string MacroSummary { get; set; } = string.Empty;
    public double ServingSizeG { get; set; }
}

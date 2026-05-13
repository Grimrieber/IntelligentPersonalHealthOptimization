using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class MealPlanViewModel : BaseViewModel
{
    private readonly INutritionService _nutritionService;
    private readonly IFoodService _foodService;
    private readonly IUserService _userService;
    private readonly IRecipeService _recipeService;

    public MealPlanViewModel(INutritionService nutritionService, IFoodService foodService,
        IUserService userService, IRecipeService recipeService)
    {
        _nutritionService = nutritionService;
        _foodService = foodService;
        _userService = userService;
        _recipeService = recipeService;
        Title = "Meal Plan";

        WeakReferenceMessenger.Default.Register<MealSelectedMessage>(this, async (r, msg) =>
        {
            await HandleMealSelectedAsync(msg);
        });
    }

    [ObservableProperty]
    private bool _hasMealPlan;

    [ObservableProperty]
    private string _planName = string.Empty;

    [ObservableProperty]
    private string _planTargetCalories = string.Empty;

    [ObservableProperty]
    private string _planTargetMacros = string.Empty;

    [ObservableProperty]
    private List<string> _dayNames = new();

    [ObservableProperty]
    private int _selectedDayIndex;

    [ObservableProperty]
    private string _selectedDayName = "Day 1";

    [ObservableProperty]
    private string _dayTotalCalories = "0 kcal";

    [ObservableProperty]
    private string _dayMacroSummary = string.Empty;

    [ObservableProperty]
    private ObservableCollection<MealPlanMealGroup> _mealPlanMealGroups = new();

    [ObservableProperty]
    private bool _hasMealsForDay;

    private MealPlan? _activePlan;
    private List<MealPlanDay> _allDays = new();

    partial void OnSelectedDayIndexChanged(int value)
    {
        if (value >= 0 && value < _allDays.Count)
        {
            LoadDayData(_allDays[value]);
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            _activePlan = await _nutritionService.GetActiveMealPlanAsync(user.Id);
            HasMealPlan = _activePlan != null;

            if (_activePlan == null) return;

            PlanName = _activePlan.PlanName;
            PlanTargetCalories = $"Target: {_activePlan.TargetCalories} kcal/day";
            PlanTargetMacros = $"P: {_activePlan.TargetProteinG}g  |  C: {_activePlan.TargetCarbsG}g  |  F: {_activePlan.TargetFatG}g";

            _allDays = await _nutritionService.GetMealPlanDaysAsync(_activePlan.Id);
            DayNames = _allDays.Select(d => d.DayName).ToList();

            if (_allDays.Count > 0)
            {
                SelectedDayIndex = 0;
                LoadDayData(_allDays[0]);
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Meal plan load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void LoadDayData(MealPlanDay day)
    {
        try
        {
            SelectedDayName = day.DayName;
            DayTotalCalories = $"{day.TotalCalories} kcal";
            DayMacroSummary = string.Empty; // will be set after loading items

            var items = await _nutritionService.GetMealItemsAsync(day.Id);
            HasMealsForDay = items.Count > 0;

            var groups = new ObservableCollection<MealPlanMealGroup>();

            // Separate protein shakes from regular food items so they get their own cards
            var shakeItems = items.Where(i => i.MealName == "Protein Shake").ToList();
            var foodItems = items.Where(i => i.MealName != "Protein Shake").ToList();

            var mealTypes = foodItems.Select(i => i.MealType).Distinct().OrderBy(m => m);

            foreach (var mealType in mealTypes)
            {
                var mealItems = foodItems.Where(i => i.MealType == mealType).OrderBy(i => i.OrderIndex).ToList();

                var displayItems = new ObservableCollection<MealPlanFoodItem>();
                foreach (var item in mealItems)
                {
                    // Recipe-based items use MealName; legacy items look up food by ID
                    string foodName;
                    if (item.SavedRecipeId > 0)
                        foodName = item.MealName;
                    else if (item.FoodId > 0)
                    {
                        var food = await _foodService.GetFoodByIdAsync(item.FoodId);
                        foodName = food?.Name ?? item.MealName;
                    }
                    else
                        foodName = item.MealName;

                    displayItems.Add(new MealPlanFoodItem
                    {
                        FoodName = foodName,
                        ServingSize = item.SavedRecipeId > 0 ? "1 serving" : $"{item.ServingSizeG:F0}g",
                        Calories = $"{item.Calories:F0} kcal",
                        Protein = $"{item.ProteinG:F0}g",
                        Carbs = $"{item.CarbsG:F0}g",
                        Fat = $"{item.FatG:F0}g",
                        MacroSummary = $"P: {item.ProteinG:F0}g  C: {item.CarbsG:F0}g  F: {item.FatG:F0}g"
                    });
                }

                var mealCalories = mealItems.Sum(i => i.Calories);
                var mealProtein = mealItems.Sum(i => i.ProteinG);
                var mealCarbs = mealItems.Sum(i => i.CarbsG);
                var mealFat = mealItems.Sum(i => i.FatG);
                var mealName = mealItems.FirstOrDefault(i => !string.IsNullOrEmpty(i.MealName))?.MealName ?? string.Empty;
                var savedRecipeId = mealItems.FirstOrDefault(i => i.SavedRecipeId > 0)?.SavedRecipeId ?? 0;

                // Calculate servings note
                var servingsNote = string.Empty;
                var firstItem = mealItems.FirstOrDefault();
                if (firstItem != null && savedRecipeId > 0)
                {
                    var s = firstItem.Servings;
                    if (s > 1.01)
                    {
                        // Round to nearest half for cleaner display
                        var rounded = Math.Round(s * 2) / 2.0;
                        var servingText = rounded % 1 == 0 ? $"{rounded:F0}" : $"{rounded:F1}";
                        servingsNote = $"Make {servingText}x this recipe";
                    }
                }

                groups.Add(new MealPlanMealGroup
                {
                    MealType = mealType,
                    MealPlanDayId = day.Id,
                    SavedRecipeId = savedRecipeId,
                    MealTypeName = FormatMealType(mealType),
                    MealName = mealName,
                    HasMealName = !string.IsNullOrEmpty(mealName),
                    TotalCalories = $"{mealCalories:F0} kcal",
                    TotalCaloriesNumeric = (int)mealCalories,
                    ServingsNote = servingsNote,
                    HasServingsNote = !string.IsNullOrEmpty(servingsNote),
                    MacroSummary = $"P: {mealProtein:F0}g  C: {mealCarbs:F0}g  F: {mealFat:F0}g",
                    Items = displayItems
                });
            }

            // Add protein shakes as separate cards
            foreach (var shake in shakeItems)
            {
                var shakeFood = await _foodService.GetFoodByIdAsync(shake.FoodId);
                var shakeDisplay = new ObservableCollection<MealPlanFoodItem>
                {
                    new()
                    {
                        FoodName = shakeFood?.Name ?? "Whey Protein Powder",
                        ServingSize = $"{shake.ServingSizeG:F0}g",
                        Calories = $"{shake.Calories:F0} kcal",
                        Protein = $"{shake.ProteinG:F0}g",
                        Carbs = $"{shake.CarbsG:F0}g",
                        Fat = $"{shake.FatG:F0}g",
                        MacroSummary = $"P: {shake.ProteinG:F0}g  C: {shake.CarbsG:F0}g  F: {shake.FatG:F0}g"
                    }
                };

                groups.Add(new MealPlanMealGroup
                {
                    MealType = shake.MealType,
                    MealPlanDayId = day.Id,
                    MealTypeName = $"Protein Shake ({FormatMealType(shake.MealType)})",
                    MealName = "Protein Shake",
                    HasMealName = true,
                    IsSwappable = false,
                    TotalCalories = $"{shake.Calories:F0} kcal",
                    TotalCaloriesNumeric = (int)shake.Calories,
                    MacroSummary = $"P: {shake.ProteinG:F0}g  C: {shake.CarbsG:F0}g  F: {shake.FatG:F0}g",
                    Items = shakeDisplay
                });
            }

            MealPlanMealGroups = groups;

            // Calculate actual day macro totals from all items
            var dayProtein = (int)items.Sum(i => i.ProteinG);
            var dayCarbs = (int)items.Sum(i => i.CarbsG);
            var dayFat = (int)items.Sum(i => i.FatG);
            DayMacroSummary = $"P: {dayProtein}g  |  C: {dayCarbs}g  |  F: {dayFat}g";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Load day data", ex);
        }
    }

    [RelayCommand]
    private void PreviousDay()
    {
        if (SelectedDayIndex > 0)
        {
            SelectedDayIndex--;
        }
    }

    [RelayCommand]
    private void NextDay()
    {
        if (SelectedDayIndex < _allDays.Count - 1)
        {
            SelectedDayIndex++;
        }
    }

    /// <summary>
    /// Ensures the NutritionProfile exists and is synced with the latest assessment data.
    /// Creates the profile if missing, updates it if assessment data is newer.
    /// </summary>
    private async Task<NutritionProfile> EnsureProfileAsync(User user)
    {
        var profile = await _nutritionService.GetNutritionProfileAsync(user.Id);
        var assessment = await _nutritionService.GetLatestAssessmentAsync(user.Id);
        var bmr = _nutritionService.CalculateBMR(user);
        var tdee = _nutritionService.CalculateTDEE(bmr, user.ActivityLevel);

        if (profile == null)
        {
            if (assessment != null && assessment.RecommendedCalories > 0)
            {
                profile = await _nutritionService.CreateNutritionProfileAsync(
                    user.Id, assessment.SelectedDietType, assessment.MealsPerDay,
                    assessment.SelectedAllergiesJson, string.Empty, assessment.FoodsToAvoidJson,
                    8, string.Empty, string.Empty, 0,
                    bmr, tdee, assessment.RecommendedCalories,
                    assessment.RecommendedProteinG, assessment.RecommendedCarbsG, assessment.RecommendedFatG);
            }
            else
            {
                var (proteinG, carbsG, fatG) = _nutritionService.CalculateMacroTargets(tdee, user.FitnessGoal);
                profile = await _nutritionService.CreateNutritionProfileAsync(
                    user.Id, DietType.Standard, 3,
                    string.Empty, string.Empty, string.Empty,
                    8, string.Empty, string.Empty, 0,
                    bmr, tdee, (int)tdee, proteinG, carbsG, fatG);
            }
        }
        else if (assessment != null && assessment.RecommendedCalories > 0)
        {
            // Sync existing profile with latest assessment data
            profile.DietType = assessment.SelectedDietType;
            profile.MealsPerDay = assessment.MealsPerDay;
            profile.Allergies = assessment.SelectedAllergiesJson;
            profile.FoodDislikes = assessment.FoodsToAvoidJson;
            profile.BMR = bmr;
            profile.TDEE = tdee;
            profile.TargetCalories = assessment.RecommendedCalories;
            profile.TargetProteinG = assessment.RecommendedProteinG;
            profile.TargetCarbsG = assessment.RecommendedCarbsG;
            profile.TargetFatG = assessment.RecommendedFatG;
            profile.UsesProteinShakes = assessment.UsesProteinShakes;
            profile.ShakesPerDay = assessment.ShakesPerDay;
            profile.ProteinPerShakeG = assessment.ProteinPerShakeG;
            await _nutritionService.UpdateNutritionProfileAsync(profile);
        }

        return profile;
    }

    [RelayCommand]
    private async Task GeneratePlanAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            // Clear today's food log
            var todayLog = await _foodService.GetFoodLogAsync(user.Id, DateTime.Today);
            foreach (var entry in todayLog)
                await _foodService.DeleteFoodLogEntryAsync(entry.Id);

            var profile = await EnsureProfileAsync(user);
            try
            {
                await _nutritionService.GenerateRecipeMealPlanAsync(user.Id, profile.Id);
            }
            catch
            {
                // Fallback to template-based if recipe generation fails
                await _nutritionService.GenerateMealPlanAsync(user.Id, profile.Id);
            }
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to generate meal plan: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegeneratePlanAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Regenerate Meal Plan",
            "This will replace your current meal plan with a new one and clear today's food log. Continue?",
            "Regenerate", "Cancel");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            // Clear today's food log so calories/macros reset to 0
            var todayLog = await _foodService.GetFoodLogAsync(user.Id, DateTime.Today);
            foreach (var entry in todayLog)
                await _foodService.DeleteFoodLogEntryAsync(entry.Id);

            var profile = await EnsureProfileAsync(user);
            try
            {
                await _nutritionService.GenerateRecipeMealPlanAsync(user.Id, profile.Id);
            }
            catch
            {
                // Fallback to template-based if recipe generation fails
                await _nutritionService.RegenerateMealPlanAsync(user.Id, profile.Id);
            }
            await LoadDataAsync();

            await Shell.Current.DisplayAlert("Success", "Your meal plan has been regenerated!", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to regenerate meal plan: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SwapMealAsync(MealPlanMealGroup group)
    {
        if (group == null || _activePlan == null) return;

        var user = await _userService.GetCurrentUserAsync();
        if (user == null) return;

        var profile = await _nutritionService.GetNutritionProfileAsync(user.Id);
        if (profile == null) return;

        // Use the actual calories of the current meal slot as the target for the replacement
        var slotCalories = group.TotalCaloriesNumeric > 0
            ? group.TotalCaloriesNumeric
            : profile.TargetCalories / Math.Max(1, profile.MealsPerDay);

        var parameters = new Dictionary<string, object>
        {
            ["mealType"] = (int)group.MealType,
            ["dietType"] = (int)profile.DietType,
            ["mealPlanDayId"] = group.MealPlanDayId,
            ["nutritionProfileId"] = profile.Id,
            ["targetCalories"] = slotCalories
        };

        await Shell.Current.GoToAsync(RouteConstants.MealSelection, parameters);
    }

    private async Task HandleMealSelectedAsync(MealSelectedMessage msg)
    {
        if (_activePlan == null) return;

        try
        {
            IsBusy = true;
            // The MealSelectionViewModel already wrote the new item to DB,
            // so just reload the day data
            _allDays = await _nutritionService.GetMealPlanDaysAsync(_activePlan.Id);
            if (SelectedDayIndex >= 0 && SelectedDayIndex < _allDays.Count)
            {
                LoadDayData(_allDays[SelectedDayIndex]);
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Swap meal", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ViewMealDetailAsync(MealPlanMealGroup group)
    {
        if (group == null) return;

        // Recipe-based meals: navigate to full recipe detail page
        if (group.SavedRecipeId > 0)
        {
            await Shell.Current.GoToAsync($"RecipeDetail?savedRecipeId={group.SavedRecipeId}");
            return;
        }

        // Template-based meals: search for a matching recipe by name in the recipe database
        // and navigate to it if found
        if (!string.IsNullOrEmpty(group.MealName) && group.MealName != "Protein Shake")
        {
            try
            {
                var user = await _userService.GetCurrentUserAsync();
                if (user != null)
                {
                    // For template meals, try to find a matching recipe in the API
                    var results = await _recipeService.SearchRecipesAsync(group.MealName);
                    if (results.Count > 0)
                    {
                        await Shell.Current.GoToAsync($"RecipeDetail?recipeId={results[0].RecipeID}");
                        return;
                    }
                }
            }
            catch { /* fall through to info display */ }
        }

        // Protein shakes or items with no recipe match: show detailed info
        var title = string.IsNullOrEmpty(group.MealName) ? group.MealTypeName : group.MealName;
        var ingredientLines = group.Items.Select(i => $"• {i.FoodName} — {i.ServingSize} ({i.Calories})");
        var totalProtein = group.Items.Sum(i => double.TryParse(i.Protein.Replace("g", ""), out var v) ? v : 0);
        var totalCarbs = group.Items.Sum(i => double.TryParse(i.Carbs.Replace("g", ""), out var v) ? v : 0);
        var totalFat = group.Items.Sum(i => double.TryParse(i.Fat.Replace("g", ""), out var v) ? v : 0);

        var message = $"{group.TotalCalories}\n\n" +
                      $"Ingredients:\n{string.Join("\n", ingredientLines)}\n\n" +
                      $"Nutrition per serving:\n" +
                      $"• Protein: {totalProtein:F0}g\n" +
                      $"• Carbs: {totalCarbs:F0}g\n" +
                      $"• Fat: {totalFat:F0}g\n\n" +
                      "Regenerate your meal plan with the recipe API running to get full recipes with cooking instructions.";

        await Shell.Current.DisplayAlert(title, message, "OK");
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

public class MealPlanMealGroup
{
    public MealType MealType { get; set; }
    public int MealPlanDayId { get; set; }
    public int SavedRecipeId { get; set; }
    public string MealTypeName { get; set; } = string.Empty;
    public string MealName { get; set; } = string.Empty;
    public bool HasMealName { get; set; }
    public string TotalCalories { get; set; } = string.Empty;
    public int TotalCaloriesNumeric { get; set; }
    public string ServingsNote { get; set; } = string.Empty;
    public bool HasServingsNote { get; set; }
    public string MacroSummary { get; set; } = string.Empty;
    public bool IsSwappable { get; set; } = true;
    public ObservableCollection<MealPlanFoodItem> Items { get; set; } = new();
}

public class MealPlanFoodItem
{
    public string FoodName { get; set; } = string.Empty;
    public string ServingSize { get; set; } = string.Empty;
    public string Calories { get; set; } = string.Empty;
    public string Protein { get; set; } = string.Empty;
    public string Carbs { get; set; } = string.Empty;
    public string Fat { get; set; } = string.Empty;
    public string MacroSummary { get; set; } = string.Empty;
}

using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class NutritionDashboardViewModel : BaseViewModel
{
    private readonly INutritionService _nutritionService;
    private readonly IFoodService _foodService;
    private readonly IUserService _userService;
    private readonly IDatabaseService _databaseService;
    private readonly IRecipeService _recipeService;

    public NutritionDashboardViewModel(INutritionService nutritionService, IFoodService foodService,
        IUserService userService, IDatabaseService databaseService, IRecipeService recipeService)
    {
        _nutritionService = nutritionService;
        _foodService = foodService;
        _userService = userService;
        _databaseService = databaseService;
        _recipeService = recipeService;
        Title = "Nutrition Coach";
    }

    [ObservableProperty]
    private double _caloriesConsumed;

    [ObservableProperty]
    private double _caloriesTarget = 2000;

    [ObservableProperty]
    private double _calorieProgress;

    [ObservableProperty]
    private string _calorieLabel = "0 / 2000 kcal";

    [ObservableProperty]
    private double _proteinConsumed;

    [ObservableProperty]
    private double _proteinTarget = 150;

    [ObservableProperty]
    private double _proteinProgress;

    [ObservableProperty]
    private string _proteinLabel = "0g / 150g";

    [ObservableProperty]
    private double _carbsConsumed;

    [ObservableProperty]
    private double _carbsTarget = 250;

    [ObservableProperty]
    private double _carbsProgress;

    [ObservableProperty]
    private string _carbsLabel = "0g / 250g";

    [ObservableProperty]
    private double _fatConsumed;

    [ObservableProperty]
    private double _fatTarget = 65;

    [ObservableProperty]
    private double _fatProgress;

    [ObservableProperty]
    private string _fatLabel = "0g / 65g";

    [ObservableProperty]
    private int _waterGlasses;

    [ObservableProperty]
    private int _waterTarget = 8;

    [ObservableProperty]
    private string _waterLabel = "0 / 8 glasses";

    [ObservableProperty]
    private ObservableCollection<MealGroup> _mealGroups = new();

    [ObservableProperty]
    private bool _hasNutritionProfile;

    [ObservableProperty]
    private bool _hasMealsToday;

    [ObservableProperty]
    private bool _hasNutritionAssessment;

    [ObservableProperty]
    private string _lastAssessmentDate = string.Empty;

    // Wellness Check-In (Eating Disorder Screening)
    [ObservableProperty]
    private bool _hasWellnessCheckIn;

    [ObservableProperty]
    private string _lastWellnessCheckInDate = string.Empty;

    // Today's Planned Meals
    [ObservableProperty] private ObservableCollection<PlannedMealItem> _todaysPlannedMeals = new();
    [ObservableProperty] private bool _hasPlannedMeals;

    // Weight & Progress
    [ObservableProperty] private bool _hasWeightGoal;
    [ObservableProperty] private string _currentWeightDisplay = "—";
    [ObservableProperty] private string _targetWeightDisplay = "—";
    [ObservableProperty] private string _startWeightDisplay = "—";
    [ObservableProperty] private string _progressPercentText = "0% complete";
    [ObservableProperty] private string _daysRemainingText = string.Empty;
    [ObservableProperty] private double _weightGoalProgress;
    [ObservableProperty] private string _weightChangeText = string.Empty;
    [ObservableProperty] private bool _hasWeightChange;
    [ObservableProperty] private string _assessmentDateRange = string.Empty;

    private static List<NutritionFocusArea> DeserializeFocusAreas(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<NutritionFocusArea>();
        try { return JsonSerializer.Deserialize<List<NutritionFocusArea>>(json) ?? new(); }
        catch { return new List<NutritionFocusArea>(); }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var profile = await _nutritionService.GetNutritionProfileAsync(user.Id);
            HasNutritionProfile = profile != null;

            // Load latest assessment first so we can recompute calorie targets live
            // (instead of using the stale snapshot stored on profile.TargetCalories
            // at the time of the last assessment completion).
            var latestAssess = await _nutritionService.GetLatestAssessmentAsync(user.Id);

            if (profile != null)
            {
                // Recompute targets fresh from CURRENT User.WeightKg + ActivityLevel
                // and the latest assessment inputs. If the user updates their weight
                // on the Goals page, the dashboard reflects it on next load.
                if (latestAssess != null && user.WeightKg > 0)
                {
                    var bmrLive = _nutritionService.CalculateBMR(user);
                    var tdeeLive = _nutritionService.CalculateTDEE(bmrLive, user.ActivityLevel);
                    var focusAreas = DeserializeFocusAreas(latestAssess.FocusAreasJson);
                    var (cal, p, c, f) = _nutritionService.CalculateAssessmentTargets(
                        tdeeLive,
                        latestAssess.PrimaryGoal,
                        profile.DietType,
                        user.Gender,
                        user.WeightKg,
                        latestAssess.TargetWeightKg,
                        latestAssess.SelectedTimeline,
                        latestAssess.WorkoutsPerWeek,
                        latestAssess.AvgWorkoutMinutes,
                        focusAreas,
                        latestAssess.ConfidenceLevel,
                        latestAssess.ReadinessScore);

                    CaloriesTarget = cal;
                    ProteinTarget = p;
                    CarbsTarget = c;
                    FatTarget = f;

                    // Persist the recomputed values so other consumers
                    // (e.g. meal planning) see the same numbers.
                    if (profile.TargetCalories != cal
                        || profile.TargetProteinG != p
                        || profile.TargetCarbsG != c
                        || profile.TargetFatG != f
                        || Math.Abs(profile.BMR - bmrLive) > 0.5
                        || Math.Abs(profile.TDEE - tdeeLive) > 0.5)
                    {
                        profile.BMR = bmrLive;
                        profile.TDEE = tdeeLive;
                        profile.TargetCalories = cal;
                        profile.TargetProteinG = p;
                        profile.TargetCarbsG = c;
                        profile.TargetFatG = f;
                        profile.UpdatedAt = DateTime.UtcNow;
                        await _databaseService.UpdateAsync(profile);
                    }
                }
                else
                {
                    // No assessment yet — fall back to the stored values
                    CaloriesTarget = profile.TargetCalories;
                    ProteinTarget = profile.TargetProteinG;
                    CarbsTarget = profile.TargetCarbsG;
                    FatTarget = profile.TargetFatG;
                }
                WaterTarget = profile.DailyWaterGlasses;
            }

            // Get DB connection for progress queries
            var db = await _databaseService.GetConnectionAsync();

            // Weight & progress display
            HasWeightGoal = latestAssess != null && latestAssess.TargetWeightKg > 0;

            if (HasWeightGoal)
            {
                var startWt = latestAssess!.RecommendedCalories > 0
                    ? user.WeightKg + (latestAssess.TargetWeightKg - user.WeightKg) // fallback
                    : user.WeightKg;

                // Use the weight at assessment time — stored on the User at that point
                // For now, estimate start weight from target + difference
                var totalToLose = Math.Abs(latestAssess.TargetWeightKg - user.WeightKg);

                // Query the first progress entry after assessment date to find start weight
                var progressEntries = await db.Table<ProgressEntry>()
                    .Where(p => p.UserId == user.Id && p.WeightKg != null)
                    .OrderBy(p => p.EntryDate)
                    .ToListAsync();

                // Start weight: first entry after assessment, or current weight as fallback
                var assessDate = latestAssess.AssessmentDate;
                var firstEntry = progressEntries.FirstOrDefault(p => p.EntryDate >= assessDate);
                startWt = firstEntry?.WeightKg ?? user.WeightKg;

                // If no progress logged yet, start weight is the user's weight when assessed
                // We can approximate: user.WeightKg is what it was at assessment if no logs exist
                if (progressEntries.Count == 0)
                    startWt = user.WeightKg;

                var totalChange = Math.Abs(startWt - latestAssess.TargetWeightKg);
                var currentChange = Math.Abs(startWt - user.WeightKg);
                var progressPct = totalChange > 0 ? Math.Min(currentChange / totalChange, 1.0) : 0;

                // Ensure direction is correct (don't show progress if going wrong way)
                var isLosing = latestAssess.TargetWeightKg < startWt;
                if (isLosing && user.WeightKg > startWt)
                    progressPct = 0; // gained weight instead of losing
                else if (!isLosing && user.WeightKg < startWt)
                    progressPct = 0; // lost weight instead of gaining

                WeightGoalProgress = progressPct;
                ProgressPercentText = $"{progressPct * 100:F0}% complete";

                CurrentWeightDisplay = $"{user.WeightKg:F1} kg / {user.WeightKg * 2.20462:F0} lb";
                TargetWeightDisplay = $"Goal: {latestAssess.TargetWeightKg:F1} kg / {latestAssess.TargetWeightKg * 2.20462:F0} lb";
                StartWeightDisplay = $"Start: {startWt:F1} kg / {startWt * 2.20462:F0} lb";

                // Days remaining
                var timelineDays = latestAssess.SelectedTimeline switch
                {
                    GoalTimeline.SixWeeks => 42,
                    GoalTimeline.EightWeeks => 56,
                    GoalTimeline.TwelveWeeks => 84,
                    GoalTimeline.SixMonths => 182,
                    _ => 84
                };
                var endDate = assessDate.AddDays(timelineDays);
                var daysLeft = (int)(endDate - DateTime.UtcNow).TotalDays;
                DaysRemainingText = daysLeft > 0 ? $"{daysLeft} days left" : "Timeline complete";

                // Weight change since start
                var changeSinceStart = user.WeightKg - startWt;
                if (Math.Abs(changeSinceStart) > 0.1)
                {
                    var changeLb = changeSinceStart * 2.20462;
                    var direction = changeSinceStart < 0 ? "lost" : "gained";
                    WeightChangeText = $"You've {direction} {Math.Abs(changeSinceStart):F1} kg ({Math.Abs(changeLb):F0} lb) since starting";
                    HasWeightChange = true;
                }
                else
                {
                    HasWeightChange = false;
                }

                // Date range
                var startStr = assessDate.ToLocalTime().ToString("MMM dd");
                var endStr = endDate.ToLocalTime().ToString("MMM dd, yyyy");
                AssessmentDateRange = $"Assessment: {startStr} - {endStr}";
            }

            var dailyTotals = await _foodService.GetDailyTotalsAsync(user.Id, DateTime.Today);
            CaloriesConsumed = dailyTotals.calories;
            ProteinConsumed = dailyTotals.proteinG;
            CarbsConsumed = dailyTotals.carbsG;
            FatConsumed = dailyTotals.fatG;

            UpdateProgressValues();

            // Check for nutrition assessment
            var latestAssessment = await db.Table<Models.NutritionAssessment>()
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.AssessmentDate)
                .FirstOrDefaultAsync();
            HasNutritionAssessment = latestAssessment != null;
            LastAssessmentDate = latestAssessment?.AssessmentDate.ToLocalTime().ToString("MMM dd, yyyy") ?? string.Empty;

            // Check for wellness check-in
            var latestWellness = await db.Table<Models.WellnessCheckIn>()
                .Where(w => w.UserId == user.Id)
                .OrderByDescending(w => w.CheckInDate)
                .FirstOrDefaultAsync();
            HasWellnessCheckIn = latestWellness != null;
            LastWellnessCheckInDate = latestWellness?.CheckInDate.ToLocalTime().ToString("MMM dd, yyyy") ?? string.Empty;

            // Load today's food log to check which planned meals are already logged
            // Clean up any duplicate entries (from past bugs with multi-clicking)
            var todayLog = await _foodService.GetFoodLogAsync(user.Id, DateTime.Today);
            var seen = new HashSet<string>();
            foreach (var entry in todayLog.ToList())
            {
                var key = $"{entry.MealType}|{entry.Notes}";
                if (!seen.Add(key))
                {
                    // Duplicate — remove it
                    await _foodService.DeleteFoodLogEntryAsync(entry.Id);
                    todayLog.Remove(entry);
                }
            }

            // Load today's planned meals from the active meal plan
            // Group by meal type so template meals (multiple ingredients) show as one card

            var plannedItems = await _nutritionService.GetTodaysMealPlanItemsAsync(user.Id);
            TodaysPlannedMeals.Clear();

            var foodItems = plannedItems.Where(i => i.MealName != "Protein Shake").ToList();
            var shakeItems = plannedItems.Where(i => i.MealName == "Protein Shake").ToList();

            // Meal-prep label for today's meals (same meals repeat across the block).
            var prepActive = (latestAssessment?.MealPrepMode ?? false) && (latestAssessment?.MealPrepDays ?? 1) > 1;
            var prepDays = Math.Max(1, latestAssessment?.MealPrepDays ?? 1);
            var isCookDay = true;
            var mealPrepNote = string.Empty;
            if (prepActive)
            {
                var activePlan = await _nutritionService.GetActiveMealPlanAsync(user.Id);
                var daysSinceStart = activePlan != null
                    ? Math.Max(0, (DateTime.UtcNow.Date - activePlan.CreatedAt.Date).Days)
                    : 0;
                var dayInBlock = daysSinceStart % prepDays;
                isCookDay = dayInBlock == 0;
                mealPrepNote = isCookDay
                    ? $"Meal prep · cook today, covers {prepDays} days"
                    : $"Meal prep · day {dayInBlock + 1} of {prepDays} (leftovers)";
            }

            // Group food items by meal type
            foreach (var group in foodItems.GroupBy(i => i.MealType))
            {
                var items = group.ToList();
                var totalCal = items.Sum(i => i.Calories);
                var totalP = items.Sum(i => i.ProteinG);
                var totalC = items.Sum(i => i.CarbsG);
                var totalF = items.Sum(i => i.FatG);
                var mealName = items.FirstOrDefault(i => !string.IsNullOrEmpty(i.MealName))?.MealName ?? FormatMealType(group.Key);
                var savedRecipeId = items.FirstOrDefault(i => i.SavedRecipeId > 0)?.SavedRecipeId ?? 0;
                var servings = items.FirstOrDefault()?.Servings ?? 1;

                // Servings note. Meal prep → batch on the cook day / reheat amount on
                // leftover days. Otherwise the per-day multiplier, only when it scales
                // (a plain "1 serving" needs no note).
                var servingsNote = string.Empty;
                if (savedRecipeId > 0)
                {
                    if (prepActive)
                    {
                        servingsNote = isCookDay
                            ? $"Cook {Math.Round(servings * prepDays, 1):0.#}x the recipe (~{servings:0.#}/day)"
                            : $"Reheat ~{servings:0.#} serving{(servings > 1.04 ? "s" : "")}";
                    }
                    else if (servings < 0.95 || servings > 1.05)
                    {
                        servingsNote = $"Make {servings:0.#}x this recipe";
                    }
                }

                // Check if this meal is already logged today
                var loggedEntry = todayLog.FirstOrDefault(l =>
                    l.MealType == group.Key &&
                    !string.IsNullOrEmpty(l.Notes) &&
                    l.Notes == mealName);

                TodaysPlannedMeals.Add(new PlannedMealItem
                {
                    MealPlanItemId = items.First().Id,
                    SavedRecipeId = savedRecipeId,
                    MealPlanDayId = items.First().MealPlanDayId,
                    IsSwappable = items.First().MealPlanDayId > 0,
                    MealTypeName = FormatMealType(group.Key),
                    RecipeName = mealName,
                    Calories = prepActive ? $"{totalCal:F0} kcal/day" : $"{totalCal:F0} kcal",
                    MacroSummary = $"P: {totalP:F0}g  C: {totalC:F0}g  F: {totalF:F0}g",
                    ServingsNote = servingsNote,
                    HasServingsNote = !string.IsNullOrEmpty(servingsNote),
                    MealPrepNote = mealPrepNote,
                    HasMealPrepNote = !string.IsNullOrEmpty(mealPrepNote),
                    CaloriesValue = totalCal,
                    ProteinValue = totalP,
                    CarbsValue = totalC,
                    FatValue = totalF,
                    MealType = group.Key,
                    IsLogged = loggedEntry != null,
                    FoodLogEntryId = loggedEntry?.Id ?? 0
                });
            }

            // Add protein shakes
            foreach (var shake in shakeItems)
            {
                var shakeLogged = todayLog.FirstOrDefault(l =>
                    l.Notes == "Protein Shake" && l.MealType == shake.MealType);

                // Tag plant-protein shakes so vegan/dairy-free users can see the
                // powder is plant-based (pea), not whey.
                var shakeFood = shake.FoodId > 0 ? await _foodService.GetFoodByIdAsync(shake.FoodId) : null;
                var powderBadge = shakeFood != null && shakeFood.Name.Contains("Pea", StringComparison.OrdinalIgnoreCase)
                    ? "Vegan · Pea Protein"
                    : string.Empty;

                TodaysPlannedMeals.Add(new PlannedMealItem
                {
                    MealPlanItemId = shake.Id,
                    MealTypeName = $"Protein Shake ({FormatMealType(shake.MealType)})",
                    RecipeName = "Protein Shake",
                    Calories = $"{shake.Calories:F0} kcal",
                    MacroSummary = $"P: {shake.ProteinG:F0}g  C: {shake.CarbsG:F0}g  F: {shake.FatG:F0}g",
                    DietBadge = powderBadge,
                    CaloriesValue = shake.Calories,
                    ProteinValue = shake.ProteinG,
                    CarbsValue = shake.CarbsG,
                    FatValue = shake.FatG,
                    MealType = shake.MealType,
                    IsLogged = shakeLogged != null,
                    FoodLogEntryId = shakeLogged?.Id ?? 0
                });
            }
            // Find logged meals that don't match any planned meal (e.g., swapped away)
            var matchedLogIds = TodaysPlannedMeals
                .Where(m => m.FoodLogEntryId > 0)
                .Select(m => m.FoodLogEntryId)
                .ToHashSet();

            foreach (var orphan in todayLog.Where(l => !matchedLogIds.Contains(l.Id)))
            {
                TodaysPlannedMeals.Add(new PlannedMealItem
                {
                    MealPlanItemId = 0,
                    SavedRecipeId = orphan.SavedRecipeId,
                    MealTypeName = FormatMealType(orphan.MealType),
                    RecipeName = !string.IsNullOrEmpty(orphan.Notes) ? orphan.Notes : "Logged Meal",
                    Calories = $"{orphan.Calories:F0} kcal",
                    MacroSummary = $"P: {orphan.ProteinG:F0}g  C: {orphan.CarbsG:F0}g  F: {orphan.FatG:F0}g",
                    CaloriesValue = orphan.Calories,
                    ProteinValue = orphan.ProteinG,
                    CarbsValue = orphan.CarbsG,
                    FatValue = orphan.FatG,
                    MealType = orphan.MealType,
                    IsLogged = true,
                    FoodLogEntryId = orphan.Id
                });
            }

            // Sort: unlogged first, logged at bottom
            var sortedMeals = TodaysPlannedMeals.OrderBy(m => m.IsLogged ? 1 : 0).ToList();
            TodaysPlannedMeals.Clear();
            foreach (var m in sortedMeals)
                TodaysPlannedMeals.Add(m);

            HasPlannedMeals = TodaysPlannedMeals.Count > 0;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Nutrition dashboard load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateProgressValues()
    {
        CalorieProgress = CaloriesTarget > 0 ? Math.Min(CaloriesConsumed / CaloriesTarget, 1.0) : 0;
        CalorieLabel = $"{CaloriesConsumed:F0} / {CaloriesTarget:F0} kcal";

        ProteinProgress = ProteinTarget > 0 ? Math.Min(ProteinConsumed / ProteinTarget, 1.0) : 0;
        ProteinLabel = $"{ProteinConsumed:F0}g / {ProteinTarget:F0}g";

        CarbsProgress = CarbsTarget > 0 ? Math.Min(CarbsConsumed / CarbsTarget, 1.0) : 0;
        CarbsLabel = $"{CarbsConsumed:F0}g / {CarbsTarget:F0}g";

        FatProgress = FatTarget > 0 ? Math.Min(FatConsumed / FatTarget, 1.0) : 0;
        FatLabel = $"{FatConsumed:F0}g / {FatTarget:F0}g";

        WaterLabel = $"{WaterGlasses} / {WaterTarget} glasses";
    }

    private async Task BuildMealGroupsAsync(List<FoodLogEntry> entries)
    {
        try
        {
            var groups = new ObservableCollection<MealGroup>();

            var mealTypes = new[] { MealType.Breakfast, MealType.MorningSnack, MealType.Lunch,
                MealType.AfternoonSnack, MealType.Dinner, MealType.EveningSnack,
                MealType.PreWorkout, MealType.PostWorkout };

            foreach (var mealType in mealTypes)
            {
                var mealEntries = entries.Where(e => e.MealType == mealType).ToList();
                if (mealEntries.Count > 0)
                {
                    var displayItems = new ObservableCollection<FoodLogDisplayItem>();
                    foreach (var e in mealEntries)
                    {
                        // Resolve the food/recipe name
                        string name;
                        if (!string.IsNullOrEmpty(e.Notes))
                            name = e.Notes; // Notes stores recipe name from "Log It"
                        else if (e.FoodId > 0)
                        {
                            var food = await _foodService.GetFoodByIdAsync(e.FoodId);
                            name = food?.Name ?? $"Food #{e.FoodId}";
                        }
                        else
                            name = FormatMealType(e.MealType);

                        displayItems.Add(new FoodLogDisplayItem
                        {
                            Id = e.Id,
                            FoodName = name,
                            ServingSize = e.ServingSizeG > 0 ? $"{e.ServingSizeG:F0}g" : string.Empty,
                            Calories = $"{e.Calories:F0} kcal",
                            Macros = $"P: {e.ProteinG:F0}g  C: {e.CarbsG:F0}g  F: {e.FatG:F0}g"
                        });
                    }

                    groups.Add(new MealGroup
                    {
                        MealType = mealType,
                        MealTypeName = FormatMealType(mealType),
                        TotalCalories = $"{mealEntries.Sum(e => e.Calories):F0} kcal",
                        Items = displayItems
                    });
                }
            }

            MealGroups = groups;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("BuildMealGroups", ex);
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

    [RelayCommand]
    private void AddWater()
    {
        WaterGlasses = Math.Min(WaterGlasses + 1, 20);
        WaterLabel = $"{WaterGlasses} / {WaterTarget} glasses";
    }

    [RelayCommand]
    private void RemoveWater()
    {
        WaterGlasses = Math.Max(WaterGlasses - 1, 0);
        WaterLabel = $"{WaterGlasses} / {WaterTarget} glasses";
    }

    [RelayCommand]
    private async Task LogFoodAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.AddFoodEntry);
    }

    [RelayCommand]
    private async Task ViewMealPlanAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.MealPlanView);
    }

    [RelayCommand]
    private async Task ViewFoodLogAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.FoodLog);
    }

    [RelayCommand]
    private async Task StartNutritionAssessmentAsync()
    {
        if (HasNutritionAssessment)
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "New Assessment",
                "Starting a new nutrition assessment will create a new progress timeline. " +
                "Your current progress history will be preserved, but your calorie targets " +
                "and meal plan will be recalculated based on the new assessment.",
                "Start New Assessment",
                "Cancel");

            if (!confirmed) return;
        }

        await Shell.Current.GoToAsync(RouteConstants.NutriAssessIntro);
    }

    [RelayCommand]
    private async Task StartWellnessCheckInAsync()
    {
        if (HasWellnessCheckIn)
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "New Check-In",
                "Would you like to take the wellness check-in again? " +
                "This helps us make sure your nutrition experience is right for you.",
                "Start Check-In",
                "Cancel");

            if (!confirmed) return;
        }

        await Shell.Current.GoToAsync(Constants.RouteConstants.WellnessIntro);
    }

    [RelayCommand]
    private async Task LogProgressAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.AddProgress);
    }

    [RelayCommand]
    private async Task ViewPlannedMealAsync(PlannedMealItem meal)
    {
        if (meal == null) return;

        // Recipe-based: navigate to saved recipe detail
        if (meal.SavedRecipeId > 0)
        {
            await Shell.Current.GoToAsync($"RecipeDetail?savedRecipeId={meal.SavedRecipeId}");
            return;
        }

        // Template/legacy meals with no SavedRecipeId: match a bundled recipe by
        // name and open it (the catalog is local — no network involved).
        if (!string.IsNullOrEmpty(meal.RecipeName) && meal.RecipeName != "Protein Shake")
        {
            try
            {
                var results = await _recipeService.SearchRecipesAsync(meal.RecipeName);
                if (results.Count > 0)
                {
                    await Shell.Current.GoToAsync($"RecipeDetail?recipeId={results[0].RecipeID}");
                    return;
                }
            }
            catch { /* no match — fall through to the info dialog */ }
        }

        // Fallback: show basic info
        await Shell.Current.DisplayAlert(meal.RecipeName,
            $"{meal.MealTypeName}\n{meal.Calories}\n{meal.MacroSummary}", "OK");
    }

    [RelayCommand]
    private async Task SwapPlannedMealAsync(PlannedMealItem meal)
    {
        if (meal == null || meal.MealPlanDayId <= 0) return;

        var user = await _userService.GetCurrentUserAsync();
        if (user == null) return;

        var profile = await _nutritionService.GetNutritionProfileAsync(user.Id);
        if (profile == null) return;

        // Target the replacement at this slot's current calories (same logic the
        // Meal Plan page uses), so the swap stays nutritionally equivalent.
        var slotCalories = meal.CaloriesValue > 0
            ? meal.CaloriesValue
            : profile.TargetCalories / Math.Max(1, profile.MealsPerDay);

        var parameters = new Dictionary<string, object>
        {
            ["mealType"] = (int)meal.MealType,
            ["dietType"] = (int)profile.DietType,
            ["mealPlanDayId"] = meal.MealPlanDayId,
            ["nutritionProfileId"] = profile.Id,
            ["targetCalories"] = slotCalories
        };

        // The MealSelection picker writes the replacement and pops back; the
        // dashboard reloads in OnAppearing, so the new meal shows automatically.
        await Shell.Current.GoToAsync(RouteConstants.MealSelection, parameters);
    }

    [RelayCommand]
    private async Task RemoveFoodLogEntryAsync(FoodLogDisplayItem item)
    {
        if (item == null) return;
        await _foodService.DeleteFoodLogEntryAsync(item.Id);
        await LoadDataAsync();
    }

    private bool _isLogging;

    [RelayCommand]
    private async Task LogPlannedMealAsync(PlannedMealItem meal)
    {
        if (meal == null || _isLogging) return;
        _isLogging = true;

        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            if (meal.IsLogged)
            {
                // Unlog: remove the food log entry
                if (meal.FoodLogEntryId > 0)
                {
                    await _foodService.DeleteFoodLogEntryAsync(meal.FoodLogEntryId);
                    meal.FoodLogEntryId = 0;
                }
                meal.IsLogged = false;
            }
            else
            {
                // Log: create food log entry
                var entry = new FoodLogEntry
                {
                    UserId = user.Id,
                    LogDate = DateTime.Today,
                    MealType = meal.MealType,
                    SavedRecipeId = meal.SavedRecipeId,
                    Calories = meal.CaloriesValue,
                    ProteinG = meal.ProteinValue,
                    CarbsG = meal.CarbsValue,
                    FatG = meal.FatValue,
                    Notes = meal.RecipeName
                };
                await _databaseService.InsertAsync(entry);
                meal.FoodLogEntryId = entry.Id;
                meal.IsLogged = true;
            }

            // Recalculate totals from the actual database to stay accurate
            var dailyTotals = await _foodService.GetDailyTotalsAsync(user.Id, DateTime.Today);
            CaloriesConsumed = dailyTotals.calories;
            ProteinConsumed = dailyTotals.proteinG;
            CarbsConsumed = dailyTotals.carbsG;
            FatConsumed = dailyTotals.fatG;
            UpdateProgressValues();

            // Re-sort: logged items go to bottom
            var sorted = TodaysPlannedMeals.OrderBy(m => m.IsLogged ? 1 : 0).ToList();
            TodaysPlannedMeals.Clear();
            foreach (var m in sorted)
                TodaysPlannedMeals.Add(m);
        }
        finally
        {
            _isLogging = false;
        }
    }

}

public class MealGroup
{
    public MealType MealType { get; set; }
    public string MealTypeName { get; set; } = string.Empty;
    public string TotalCalories { get; set; } = string.Empty;
    public ObservableCollection<FoodLogDisplayItem> Items { get; set; } = new();
}

public class FoodLogDisplayItem
{
    public int Id { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string ServingSize { get; set; } = string.Empty;
    public string Calories { get; set; } = string.Empty;
    public string Macros { get; set; } = string.Empty;
}

public partial class PlannedMealItem : ObservableObject
{
    public int MealPlanItemId { get; set; }
    public int SavedRecipeId { get; set; }
    public int MealPlanDayId { get; set; }
    /// <summary>True for real meal-plan slots (not protein shakes or orphan logged
    /// meals) — gates the "Swap" button.</summary>
    public bool IsSwappable { get; set; }
    public MealType MealType { get; set; }
    public string MealTypeName { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string Calories { get; set; } = string.Empty;
    public string MacroSummary { get; set; } = string.Empty;
    /// <summary>Optional diet tag shown on the card, e.g. "Vegan · Pea Protein" for
    /// plant-protein shakes. Empty = no badge.</summary>
    public string DietBadge { get; set; } = string.Empty;
    public bool HasDietBadge => !string.IsNullOrEmpty(DietBadge);
    public string ServingsNote { get; set; } = string.Empty;
    public bool HasServingsNote { get; set; }
    public string MealPrepNote { get; set; } = string.Empty;
    public bool HasMealPrepNote { get; set; }
    public double CaloriesValue { get; set; }
    public double ProteinValue { get; set; }
    public double CarbsValue { get; set; }
    public double FatValue { get; set; }

    // Logged state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogButtonText))]
    [NotifyPropertyChangedFor(nameof(CardColor))]
    [NotifyPropertyChangedFor(nameof(SortOrder))]
    private bool _isLogged;

    // FoodLogEntry ID for unlocking
    [ObservableProperty]
    private int _foodLogEntryId;

    public string LogButtonText => IsLogged ? "Unlog" : "Log It";
    public Color CardColor => IsLogged
        ? Color.FromArgb("#F0FFF4")
        : Color.FromArgb("#00000000");
    public int SortOrder => IsLogged ? 1 : 0;
}

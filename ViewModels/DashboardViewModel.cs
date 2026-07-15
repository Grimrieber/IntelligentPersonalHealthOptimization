using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IUserService _userService;
    private readonly IAssessmentService _assessmentService;
    private readonly IProgressService _progressService;
    private readonly IDatabaseService _databaseService;
    private readonly IScheduleService _scheduleService;
    private readonly IFoodService _foodService;
    private readonly IGoalService _goalService;
    private readonly INutritionService _nutritionService;
    private readonly INotificationService _notificationService;

    public DashboardViewModel(
        IUserService userService,
        IAssessmentService assessmentService,
        IProgressService progressService,
        IDatabaseService databaseService,
        IScheduleService scheduleService,
        IFoodService foodService,
        IGoalService goalService,
        INutritionService nutritionService,
        INotificationService notificationService)
    {
        _userService = userService;
        _assessmentService = assessmentService;
        _progressService = progressService;
        _databaseService = databaseService;
        _scheduleService = scheduleService;
        _foodService = foodService;
        _goalService = goalService;
        _nutritionService = nutritionService;
        _notificationService = notificationService;
        Title = "Dashboard";
    }

    [ObservableProperty]
    private string _greeting = string.Empty;

    // Weight stats
    [ObservableProperty]
    private string _currentWeight = "--";

    [ObservableProperty]
    private string _lastAssessmentScore = "No assessment yet";

    // Active program
    [ObservableProperty]
    private string _activeProgramName = "No active program";

    [ObservableProperty]
    private string _activeProgramPhase = string.Empty;

    [ObservableProperty]
    private bool _hasActiveProgram;

    [ObservableProperty]
    private bool _hasAssessment;

    // Today's workout
    [ObservableProperty]
    private string _todaysWorkoutName = "Rest Day";

    [ObservableProperty]
    private string _todaysWorkoutDetails = string.Empty;

    [ObservableProperty]
    private bool _hasTodaysWorkout;

    [ObservableProperty]
    private int _todaysWorkoutId;

    // Nutrition summary
    [ObservableProperty]
    private int _caloriesConsumed;

    [ObservableProperty]
    private int _caloriesTarget = 2000;

    [ObservableProperty]
    private double _caloriesProgress;

    [ObservableProperty]
    private string _caloriesText = "0 / 2000 kcal";

    // Remaining-focused framing to match the Nutrition Coach ring ("X kcal left").
    [ObservableProperty]
    private string _caloriesRemainingText = string.Empty;

    [ObservableProperty]
    private Microsoft.Maui.Graphics.Color _caloriesRemainingColor = Microsoft.Maui.Graphics.Colors.Gray;

    // Big centre value for the calorie ring (mirrors N. Coach).
    [ObservableProperty]
    private string _caloriesRemainingValue = "0";

    [ObservableProperty]
    private string _caloriesRemainingCaption = "kcal left";

    // ---- "Up Next" meal (next unlogged planned meal today) ----
    [ObservableProperty]
    private bool _hasUpNextMeal;

    [ObservableProperty]
    private string _upNextName = string.Empty;

    [ObservableProperty]
    private string _upNextMealTypeText = string.Empty;

    [ObservableProperty]
    private string _upNextCalories = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpNextImage))]
    private string? _upNextImageUrl;

    public bool HasUpNextImage => !string.IsNullOrEmpty(UpNextImageUrl);

    [ObservableProperty]
    private string _upNextEmoji = "\U0001F37D";  // 🍽

    [ObservableProperty]
    private string _upNextTier = string.Empty;

    [ObservableProperty]
    private bool _upNextHasTier;

    private int _upNextSavedRecipeId;
    private Models.Enums.MealType _upNextMealType;
    private double _upNextCal, _upNextP, _upNextC, _upNextF;
    private string _upNextLogName = string.Empty;

    [ObservableProperty]
    private int _proteinConsumed;

    [ObservableProperty]
    private int _proteinTarget = 150;

    [ObservableProperty]
    private double _proteinProgress;

    [ObservableProperty]
    private int _carbsConsumed;

    [ObservableProperty]
    private int _carbsTarget = 200;

    [ObservableProperty]
    private double _carbsProgress;

    [ObservableProperty]
    private int _fatConsumed;

    [ObservableProperty]
    private int _fatTarget = 65;

    [ObservableProperty]
    private double _fatProgress;

    [ObservableProperty]
    private bool _hasNutritionData;

    // Goals
    [ObservableProperty]
    private ObservableCollection<GoalSummaryItem> _activeGoals = [];

    [ObservableProperty]
    private bool _hasActiveGoals;

    // Streak
    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    private string _streakText = "0 day streak";

    // Stats
    [ObservableProperty]
    private string _workoutsCompletedText = "0";

    [ObservableProperty]
    private string _workoutsMissedText = "0";

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            // One-shot (self-gated): reconcile any drifted saved nutrition targets through
            // the shared entry point so the dashboard reads consistent numbers. Runs once.
            await _nutritionService.ReconcileSavedTargetsAsync(user);

            // Re-apply local notification schedule so reminders survive reboots/app updates.
            await _notificationService.RescheduleForUserAsync(user.Id);

            // Greeting
            var hour = DateTime.Now.Hour;
            var timeGreeting = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
            Greeting = $"{timeGreeting}, {user.FirstName}!";

            // Latest weight
            var latestProgress = await _progressService.GetLatestEntryAsync(user.Id);
            var weightKg = latestProgress?.WeightKg ?? user.WeightKg;
            var weightLbs = weightKg * 2.20462;
            // Two lines so it fits the narrow stat card cleanly instead of wrapping mid-value.
            CurrentWeight = $"{weightLbs:F0} lb\n{weightKg:F1} kg";

            // Latest assessment
            var latestSession = await _assessmentService.GetLatestSessionAsync(user.Id);
            HasAssessment = latestSession != null;
            LastAssessmentScore = latestSession != null
                ? $"Movement Score: {latestSession.OverallMovementScore}/100"
                : "No assessment yet";

            // Active program
            var db = await _databaseService.GetConnectionAsync();
            var activeProgram = await db.Table<WorkoutProgram>()
                .Where(p => p.UserId == user.Id && p.IsActive)
                .FirstOrDefaultAsync();

            HasActiveProgram = activeProgram != null;
            if (activeProgram != null)
            {
                ActiveProgramName = activeProgram.ProgramName;
                ActiveProgramPhase = $"{activeProgram.Phase} Phase - {activeProgram.DaysPerWeek} days/week";
            }
            else
            {
                ActiveProgramName = "No active program";
                ActiveProgramPhase = "Complete an assessment to generate your program";
            }

            // Today's workout
            await LoadTodaysWorkoutAsync(user.Id);

            // Nutrition summary
            await LoadNutritionSummaryAsync(user.Id);

            // Goals
            await LoadGoalsSummaryAsync(user.Id);

            // Streak
            await LoadStreakAsync(user.Id);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Dashboard load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadTodaysWorkoutAsync(int userId)
    {
        try
        {
            var todaysWorkout = await _scheduleService.GetTodaysWorkoutAsync(userId);
            HasTodaysWorkout = todaysWorkout?.WorkoutDayId != null;

            if (todaysWorkout != null && todaysWorkout.WorkoutDayId.HasValue)
            {
                var db = await _databaseService.GetConnectionAsync();
                var workoutDay = await db.Table<WorkoutDay>()
                    .FirstOrDefaultAsync(d => d.Id == todaysWorkout.WorkoutDayId.Value);

                TodaysWorkoutName = workoutDay?.DayName ?? "Workout";
                TodaysWorkoutDetails = todaysWorkout.Status == WorkoutStatus.Completed
                    ? "Completed!"
                    : workoutDay?.Focus ?? "Scheduled";
                TodaysWorkoutId = todaysWorkout.Id;
            }
            else
            {
                TodaysWorkoutName = "Rest Day";
                TodaysWorkoutDetails = "No workout scheduled for today";
            }
        }
        catch
        {
            TodaysWorkoutName = "Rest Day";
            TodaysWorkoutDetails = "No workout scheduled";
        }
    }

    private async Task LoadNutritionSummaryAsync(int userId)
    {
        try
        {
            var (calories, protein, carbs, fat) = await _foodService.GetDailyTotalsAsync(userId, DateTime.Today);

            CaloriesConsumed = (int)calories;
            ProteinConsumed = (int)protein;
            CarbsConsumed = (int)carbs;
            FatConsumed = (int)fat;

            // Load targets from nutrition profile
            var db = await _databaseService.GetConnectionAsync();
            var profile = await db.Table<NutritionProfile>()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile != null)
            {
                CaloriesTarget = profile.TargetCalories;
                ProteinTarget = profile.TargetProteinG;
                CarbsTarget = profile.TargetCarbsG;
                FatTarget = profile.TargetFatG;
                HasNutritionData = true;
            }

            CaloriesProgress = CaloriesTarget > 0 ? Math.Min((double)CaloriesConsumed / CaloriesTarget, 1.0) : 0;
            CaloriesText = $"{CaloriesConsumed} / {CaloriesTarget} kcal";

            // "X kcal left" (or "over") — same actionable framing as the N. Coach ring.
            if (CaloriesTarget > 0)
            {
                var remaining = CaloriesTarget - CaloriesConsumed;
                CaloriesRemainingText = remaining >= 0
                    ? $"{remaining:N0} kcal left today"
                    : $"{Math.Abs(remaining):N0} kcal over budget";
                CaloriesRemainingColor = remaining >= 0
                    ? Microsoft.Maui.Graphics.Color.FromArgb("#1F8A4C")
                    : Microsoft.Maui.Graphics.Color.FromArgb("#BB5340");
                CaloriesRemainingValue = $"{Math.Abs(remaining):N0}";
                CaloriesRemainingCaption = remaining >= 0 ? "kcal left" : "kcal over";
            }
            else
            {
                CaloriesRemainingText = string.Empty;
                CaloriesRemainingValue = $"{CaloriesConsumed}";
                CaloriesRemainingCaption = "kcal eaten";
            }

            await LoadUpNextMealAsync(userId);
            ProteinProgress = ProteinTarget > 0 ? Math.Min((double)ProteinConsumed / ProteinTarget, 1.0) : 0;
            CarbsProgress = CarbsTarget > 0 ? Math.Min((double)CarbsConsumed / CarbsTarget, 1.0) : 0;
            FatProgress = FatTarget > 0 ? Math.Min((double)FatConsumed / FatTarget, 1.0) : 0;
        }
        catch
        {
            HasNutritionData = false;
        }
    }

    /// <summary>Find the next unlogged planned meal for today and surface it as a
    /// hero card (dish photo, name, calories, quick log).</summary>
    private async Task LoadUpNextMealAsync(int userId)
    {
        try
        {
            HasUpNextMeal = false;
            var items = await _nutritionService.GetTodaysMealPlanItemsAsync(userId);
            if (items == null || items.Count == 0) return;

            var todayLog = await _foodService.GetFoodLogAsync(userId, DateTime.Today);
            var loggedNames = todayLog
                .Where(l => !string.IsNullOrEmpty(l.Notes))
                .Select(l => l.Notes!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Group the plan into one entry per meal type (template meals span rows).
            var meals = items
                .GroupBy(i => i.MealType)
                .Select(g => new
                {
                    MealType = g.Key,
                    Name = g.First().MealName,
                    SavedRecipeId = g.FirstOrDefault(x => x.SavedRecipeId > 0)?.SavedRecipeId ?? 0,
                    Cal = g.Sum(x => x.Calories),
                    P = g.Sum(x => x.ProteinG),
                    C = g.Sum(x => x.CarbsG),
                    F = g.Sum(x => x.FatG),
                })
                .OrderBy(m => (int)m.MealType)
                .ToList();

            var next = meals.FirstOrDefault(m => !loggedNames.Contains(m.Name));
            if (next == null) return;  // everything logged — hide the card

            // Dish photo + tier (same source the meal cards use).
            string? img = null, tier = null;
            if (next.SavedRecipeId > 0)
            {
                var meta = await _nutritionService.GetRecipeCardMetaAsync(new[] { next.SavedRecipeId });
                if (meta.TryGetValue(next.SavedRecipeId, out var m)) { img = m.ImageUrl; tier = m.HealthTier; }
            }

            UpNextName = next.Name;
            UpNextMealTypeText = FormatMealType(next.MealType).ToUpperInvariant();
            UpNextCalories = $"{next.Cal:F0} kcal";
            UpNextImageUrl = img;
            UpNextEmoji = next.Name.Contains("Shake", StringComparison.OrdinalIgnoreCase) ? "\U0001F964" : "\U0001F37D";
            UpNextTier = tier ?? string.Empty;
            UpNextHasTier = !string.IsNullOrEmpty(tier);

            _upNextSavedRecipeId = next.SavedRecipeId;
            _upNextMealType = next.MealType;
            _upNextCal = next.Cal; _upNextP = next.P; _upNextC = next.C; _upNextF = next.F;
            _upNextLogName = next.Name;
            HasUpNextMeal = true;
        }
        catch
        {
            HasUpNextMeal = false;
        }
    }

    /// <summary>Open the Up Next recipe.</summary>
    [RelayCommand]
    private async Task ViewUpNextAsync()
    {
        if (_upNextSavedRecipeId > 0)
            await Shell.Current.GoToAsync($"RecipeDetail?savedRecipeId={_upNextSavedRecipeId}");
    }

    /// <summary>Log the Up Next meal to today (mirrors the N. Coach "Log It" action).</summary>
    [RelayCommand]
    private async Task LogUpNextAsync()
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var entry = new FoodLogEntry
            {
                UserId = user.Id,
                LogDate = DateTime.Today,
                MealType = _upNextMealType,
                SavedRecipeId = _upNextSavedRecipeId,
                Calories = _upNextCal,
                ProteinG = _upNextP,
                CarbsG = _upNextC,
                FatG = _upNextF,
                Notes = _upNextLogName,
            };
            await _databaseService.InsertAsync(entry);

            // Refresh nutrition + advance to the next meal.
            await LoadNutritionSummaryAsync(user.Id);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Log up-next meal", ex);
        }
    }

    private static string FormatMealType(Models.Enums.MealType mealType) => mealType switch
    {
        Models.Enums.MealType.Breakfast => "Breakfast",
        Models.Enums.MealType.MorningSnack => "Morning Snack",
        Models.Enums.MealType.Lunch => "Lunch",
        Models.Enums.MealType.AfternoonSnack => "Afternoon Snack",
        Models.Enums.MealType.Dinner => "Dinner",
        Models.Enums.MealType.EveningSnack => "Evening Snack",
        Models.Enums.MealType.PreWorkout => "Pre-Workout",
        Models.Enums.MealType.PostWorkout => "Post-Workout",
        _ => mealType.ToString()
    };

    private async Task LoadGoalsSummaryAsync(int userId)
    {
        try
        {
            var goals = await _goalService.GetActiveGoalsAsync(userId);
            var goalItems = goals.Take(3).Select(g => new GoalSummaryItem
            {
                Id = g.Id,
                Title = g.Title,
                Category = g.GoalCategory.ToString(),
                ProgressPercentage = _goalService.GetGoalProgressPercentage(g),
                ProgressText = $"{_goalService.GetGoalProgressPercentage(g):F0}%"
            }).ToList();

            ActiveGoals = new ObservableCollection<GoalSummaryItem>(goalItems);
            HasActiveGoals = goalItems.Count > 0;
        }
        catch
        {
            HasActiveGoals = false;
        }
    }

    private async Task LoadStreakAsync(int userId)
    {
        try
        {
            CurrentStreak = await _scheduleService.GetStreakAsync(userId);
            StreakText = CurrentStreak == 1 ? "1 day streak" : $"{CurrentStreak} day streak";

            var (completed, missed, _, _) = await _scheduleService.GetCompletionStatsAsync(userId, 30);
            WorkoutsCompletedText = completed.ToString();
            WorkoutsMissedText = missed.ToString();
        }
        catch
        {
            CurrentStreak = 0;
            StreakText = "0 day streak";
        }
    }

    [RelayCommand]
    private async Task LogProgressAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.AddProgress);
    }

    [RelayCommand]
    private async Task ViewWorkoutAsync()
    {
        await Shell.Current.GoToAsync($"//{RouteConstants.Workout}");
    }

    [RelayCommand]
    private async Task LogFoodAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.AddFoodEntry);
    }

    [RelayCommand]
    private async Task ViewNutritionAsync()
    {
        await Shell.Current.GoToAsync($"//{RouteConstants.Nutrition}");
    }

    [RelayCommand]
    private async Task ViewGoalsAsync()
    {
        await Shell.Current.GoToAsync($"//{RouteConstants.Progress}");
    }

    [RelayCommand]
    private async Task StartTodaysWorkoutAsync()
    {
        if (HasActiveProgram)
            await Shell.Current.GoToAsync($"//{RouteConstants.Workout}");
    }
}

public class GoalSummaryItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
    public string ProgressText { get; set; } = "0%";
    public double ProgressBarValue => ProgressPercentage / 100.0;
}

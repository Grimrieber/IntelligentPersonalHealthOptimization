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

    public DashboardViewModel(
        IUserService userService,
        IAssessmentService assessmentService,
        IProgressService progressService,
        IDatabaseService databaseService,
        IScheduleService scheduleService,
        IFoodService foodService,
        IGoalService goalService,
        INutritionService nutritionService)
    {
        _userService = userService;
        _assessmentService = assessmentService;
        _progressService = progressService;
        _databaseService = databaseService;
        _scheduleService = scheduleService;
        _foodService = foodService;
        _goalService = goalService;
        _nutritionService = nutritionService;
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
            ProteinProgress = ProteinTarget > 0 ? Math.Min((double)ProteinConsumed / ProteinTarget, 1.0) : 0;
            CarbsProgress = CarbsTarget > 0 ? Math.Min((double)CarbsConsumed / CarbsTarget, 1.0) : 0;
            FatProgress = FatTarget > 0 ? Math.Min((double)FatConsumed / FatTarget, 1.0) : 0;
        }
        catch
        {
            HasNutritionData = false;
        }
    }

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

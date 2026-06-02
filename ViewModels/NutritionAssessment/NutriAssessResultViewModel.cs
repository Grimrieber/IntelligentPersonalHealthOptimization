using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class NutriAssessResultViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;
    private readonly IUserService _userService;
    private readonly INutritionService _nutritionService;

    public NutriAssessResultViewModel(
        INutritionAssessmentCoordinator coordinator,
        IUserService userService,
        INutritionService nutritionService)
    {
        _coordinator = coordinator;
        _userService = userService;
        _nutritionService = nutritionService;
        Title = _coordinator.StepTitle;
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    // Diet Plan
    [ObservableProperty] private string _dietTypeDisplay = "—";
    [ObservableProperty] private string _mealsPerDayDisplay = "—";
    [ObservableProperty] private bool _hasAllergies;
    [ObservableProperty] private bool _hasProteinShakes;
    [ObservableProperty] private string _proteinShakeDisplay = string.Empty;
    public ObservableCollection<string> AllergyDisplayItems { get; } = [];

    // Body Composition Summary
    [ObservableProperty] private bool _hasBodyCompData;
    [ObservableProperty] private string _bodyFatDisplay = "—";
    [ObservableProperty] private string _waistDisplay = "—";
    [ObservableProperty] private string _hipsDisplay = "—";
    [ObservableProperty] private string _circumferenceSummary = string.Empty;
    [ObservableProperty] private string _bodyFatDisclaimer = string.Empty;

    // Eating Patterns
    [ObservableProperty] private bool _hasEatingPatterns;
    public ObservableCollection<string> EatingPatternDisplayItems { get; } = [];

    // Readiness
    [ObservableProperty] private double _readinessScore;
    [ObservableProperty] private string _readinessDisplay = "—";
    [ObservableProperty] private string _readinessInterpretation = string.Empty;
    [ObservableProperty] private int _confidenceLevel;
    [ObservableProperty] private int _motivationCount;
    [ObservableProperty] private int _challengeCount;

    // Goals
    [ObservableProperty] private string _primaryGoalDisplay = "—";
    [ObservableProperty] private string _timelineDisplay = "—";
    [ObservableProperty] private bool _hasTargetWeight;
    [ObservableProperty] private string _targetWeightDisplay = "—";
    [ObservableProperty] private bool _hasFocusAreas;
    public ObservableCollection<string> FocusAreaDisplayItems { get; } = [];

    // Planned Exercise
    [ObservableProperty] private bool _hasExercisePlan;
    [ObservableProperty] private string _exercisePlanDisplay = "—";
    [ObservableProperty] private string _exerciseCaloriesDisplay = "—";

    // Macro Targets
    [ObservableProperty] private string _caloriesDisplay = "—";
    [ObservableProperty] private int _recommendedProteinG;
    [ObservableProperty] private int _recommendedCarbsG;
    [ObservableProperty] private int _recommendedFatG;
    [ObservableProperty] private string _macroNote = string.Empty;

    // Safety Warning
    [ObservableProperty] private bool _showSafetyWarning;
    [ObservableProperty] private string _safetyWarningText = string.Empty;

    // Meal Breakdown
    public ObservableCollection<MealCalorieItem> MealBreakdownItems { get; } = [];

    [RelayCommand]
    private async Task LoadResultsAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var data = _coordinator.Data;

            // Diet Plan
            DietTypeDisplay = SplitCamelCase(data.SelectedDietType.ToString());
            MealsPerDayDisplay = $"{data.MealsPerDay} meals/day";
            HasProteinShakes = data.UsesProteinShakes;
            if (data.UsesProteinShakes)
            {
                var totalShakeProtein = data.ShakesPerDay * data.ProteinPerShakeG;
                ProteinShakeDisplay = $"{data.ShakesPerDay}x {data.ProteinPerShakeG}g protein ({totalShakeProtein}g total)";
            }
            AllergyDisplayItems.Clear();
            foreach (var allergy in data.SelectedAllergies.Where(a => a != FoodAllergy.None))
                AllergyDisplayItems.Add(SplitCamelCase(allergy.ToString()));
            foreach (var food in data.FoodsToAvoid)
                AllergyDisplayItems.Add(food);
            HasAllergies = AllergyDisplayItems.Count > 0;

            // Body Composition (Navy formula — calculated from waist, hips, neck, height)
            HasBodyCompData = data.WaistCm > 0 && data.NeckCm > 0;
            if (HasBodyCompData)
            {
                var bodyFat = CalculateBodyFatNavy(
                    data.WaistCm, data.HipsCm, data.NeckCm,
                    user.HeightCm, user.Gender);
                BodyFatDisplay = bodyFat > 0 ? $"{bodyFat:F1}%" : "—";
                BodyFatDisclaimer = "Estimated using the US Navy formula. This method uses circumference measurements and may overestimate body fat for muscular or athletic individuals. For a more accurate reading, consider a DEXA scan or hydrostatic weighing.";
            }
            WaistDisplay = data.WaistCm > 0 ? $"{data.WaistCm:F1} cm" : "—";
            HipsDisplay = data.HipsCm > 0 ? $"{data.HipsCm:F1} cm" : "—";

            var measurements = new List<string>();
            if (data.NeckCm > 0) measurements.Add($"Neck: {data.NeckCm:F1} cm");
            if (data.ChestCm > 0) measurements.Add($"Chest: {data.ChestCm:F1} cm");
            if (data.BicepCm > 0) measurements.Add($"Bicep: {data.BicepCm:F1} cm");
            if (data.ThighCm > 0) measurements.Add($"Thigh: {data.ThighCm:F1} cm");
            if (data.CalfCm > 0) measurements.Add($"Calf: {data.CalfCm:F1} cm");
            CircumferenceSummary = string.Join(" | ", measurements);

            // Eating Patterns
            EatingPatternDisplayItems.Clear();
            foreach (var p in data.SelectedEatingPatterns)
                EatingPatternDisplayItems.Add(SplitCamelCase(p.ToString()));
            HasEatingPatterns = EatingPatternDisplayItems.Count > 0;

            // Readiness
            ConfidenceLevel = data.ConfidenceLevel;
            MotivationCount = data.SelectedMotivations.Count;
            ChallengeCount = data.SelectedChallenges.Count;

            var confidenceComponent = data.ConfidenceLevel / 10.0 * 5.0;
            var total = MotivationCount + ChallengeCount;
            var balanceComponent = total > 0
                ? (double)MotivationCount / total * 5.0
                : 2.5;
            ReadinessScore = Math.Round(confidenceComponent + balanceComponent, 1);
            ReadinessDisplay = $"{ReadinessScore:F1}/10";
            ReadinessInterpretation = ReadinessScore switch
            {
                <= 3 => "Pre-contemplation: You may benefit from exploring your motivations further before making major changes.",
                <= 6 => "Preparation: You're getting ready for change. Start with small, achievable steps.",
                _ => "Action-ready: You have strong motivation and a positive outlook. Time to implement your plan!"
            };

            // Goals & Focus Areas
            PrimaryGoalDisplay = SplitCamelCase(data.PrimaryGoal.ToString());
            TimelineDisplay = data.SelectedTimeline switch
            {
                GoalTimeline.SixWeeks => "6 weeks",
                GoalTimeline.EightWeeks => "8 weeks",
                GoalTimeline.TwelveWeeks => "12 weeks",
                GoalTimeline.SixMonths => "6 months",
                _ => SplitCamelCase(data.SelectedTimeline.ToString())
            };
            HasTargetWeight = data.TargetWeightKg > 0;
            TargetWeightDisplay = HasTargetWeight
                ? $"{data.TargetWeightKg:F1} kg ({data.TargetWeightKg * 2.20462:F0} lb)"
                : "—";

            FocusAreaDisplayItems.Clear();
            foreach (var area in data.SelectedFocusAreas)
                FocusAreaDisplayItems.Add(SplitCamelCase(area.ToString()));
            HasFocusAreas = FocusAreaDisplayItems.Count > 0;

            // Planned Exercise
            HasExercisePlan = data.WorkoutsPerWeek > 0;
            if (HasExercisePlan)
            {
                ExercisePlanDisplay = $"{data.WorkoutsPerWeek}x/week, {data.AvgWorkoutMinutes} min each";
                var calPerMinute = 6.0 * 3.5 * user.WeightKg / 200.0;
                var dailyExerciseCal = data.WorkoutsPerWeek * data.AvgWorkoutMinutes * calPerMinute / 7.0;
                ExerciseCaloriesDisplay = $"~{(int)dailyExerciseCal} kcal/day from exercise";
            }

            // Safety Warning — flag if weekly weight change rate exceeds safe limits
            ShowSafetyWarning = false;
            if (data.TargetWeightKg > 0 && user.WeightKg > 0)
            {
                var diffKg = Math.Abs(data.TargetWeightKg - user.WeightKg);
                var isLoss = data.TargetWeightKg < user.WeightKg;
                var timelineWeeks = data.SelectedTimeline switch
                {
                    GoalTimeline.SixWeeks => 6,
                    GoalTimeline.EightWeeks => 8,
                    GoalTimeline.TwelveWeeks => 12,
                    GoalTimeline.SixMonths => 26,
                    _ => 12
                };
                var weeklyRate = diffKg / timelineWeeks;
                var safeRate = isLoss ? 1.0 : 0.5;
                if (weeklyRate > safeRate && diffKg > 0.5)
                {
                    var safeWeeks = (int)Math.Ceiling(diffKg / safeRate);
                    var direction = isLoss ? "loss" : "gain";
                    SafetyWarningText = $"Your current plan requires {weeklyRate:F1} kg/week of weight {direction}, " +
                                        $"which exceeds the recommended safe rate of {safeRate:F1} kg/week. " +
                                        $"A minimum of {safeWeeks} weeks is recommended for this goal. " +
                                        $"Consider adjusting your timeline or target weight.";
                    ShowSafetyWarning = true;
                }
            }

            // Macro Targets driven by assessment goal + diet type + target weight + exercise
            var bmr = _nutritionService.CalculateBMR(user);
            var tdee = _nutritionService.CalculateTDEE(bmr, user.ActivityLevel);
            var readinessScore = ReadinessScore;
            var (targetCalories, proteinG, carbsG, fatG) = _nutritionService.CalculateAssessmentTargets(
                tdee, data.PrimaryGoal, data.SelectedDietType, user.Gender,
                user.WeightKg, data.TargetWeightKg, data.SelectedTimeline,
                data.WorkoutsPerWeek, data.AvgWorkoutMinutes,
                data.SelectedFocusAreas, data.ConfidenceLevel, readinessScore);

            // === DIAGNOSTIC: write to file for adb run-as to read ===
            try
            {
                var ageNow = DateTime.Today.Year - user.DateOfBirth.Year;
                if (user.DateOfBirth.Date > DateTime.Today.AddYears(-ageNow)) ageNow--;
                var path = System.IO.Path.Combine(FileSystem.AppDataDirectory, "calorie_diag.txt");
                var dump =
                    $"===== ResultPage load @ {DateTime.Now:HH:mm:ss} =====\n" +
                    $"user.WeightKg = {user.WeightKg}\n" +
                    $"user.HeightCm = {user.HeightCm}\n" +
                    $"user.DateOfBirth = {user.DateOfBirth:yyyy-MM-dd} (age {ageNow})\n" +
                    $"user.Gender = {user.Gender}\n" +
                    $"user.ActivityLevel = {user.ActivityLevel}\n" +
                    $"data.PrimaryGoal = {data.PrimaryGoal}\n" +
                    $"data.TargetWeightKg = {data.TargetWeightKg}\n" +
                    $"data.SelectedTimeline = {data.SelectedTimeline}\n" +
                    $"data.SelectedDietType = {data.SelectedDietType}\n" +
                    $"data.WorkoutsPerWeek = {data.WorkoutsPerWeek}\n" +
                    $"data.AvgWorkoutMinutes = {data.AvgWorkoutMinutes}\n" +
                    $"data.ConfidenceLevel = {data.ConfidenceLevel}\n" +
                    $"readinessScore = {readinessScore}\n" +
                    $"focusAreas = [{string.Join(",", data.SelectedFocusAreas)}]\n" +
                    $"BMR = {bmr:F1}\n" +
                    $"TDEE = {tdee:F1}\n" +
                    $"TARGET = {targetCalories}\n" +
                    $"protein/carbs/fat = {proteinG}/{carbsG}/{fatG}\n" +
                    $"====================\n";
                System.IO.File.WriteAllText(path, dump);
            }
            catch { /* best effort */ }

            CaloriesDisplay = $"{targetCalories} kcal/day";
            RecommendedProteinG = proteinG;
            RecommendedCarbsG = carbsG;
            RecommendedFatG = fatG;

            // Build descriptive macro note — use exercise-adjusted TDEE for accurate diff
            var effectiveTdee = tdee;
            if (data.WorkoutsPerWeek > 0 && data.AvgWorkoutMinutes > 0)
            {
                var calPerMin = 6.0 * 3.5 * user.WeightKg / 200.0;
                effectiveTdee += data.WorkoutsPerWeek * data.AvgWorkoutMinutes * calPerMin / 7.0;
            }
            var goalLabel = SplitCamelCase(data.PrimaryGoal.ToString());
            var dailyDiff = targetCalories - (int)effectiveTdee;
            string calorieAdjustment;
            if (data.TargetWeightKg > 0 && user.WeightKg > 0)
            {
                var weightDiff = data.TargetWeightKg - user.WeightKg;
                var direction = weightDiff < 0 ? "lose" : "gain";
                calorieAdjustment = $"{Math.Abs(dailyDiff)} kcal/day {(dailyDiff < 0 ? "deficit" : "surplus")} to {direction} {Math.Abs(weightDiff):F1} kg in {TimelineDisplay.ToLower()}";
            }
            else
            {
                calorieAdjustment = dailyDiff == 0
                    ? "maintenance calories"
                    : $"{Math.Abs(dailyDiff)} kcal {(dailyDiff < 0 ? "deficit" : "surplus")}";
            }
            MacroNote = data.SelectedDietType != DietType.Standard
                ? $"{SplitCamelCase(data.SelectedDietType.ToString())} diet macros. {calorieAdjustment}."
                : $"{goalLabel}: {calorieAdjustment}.";

            // Meal Breakdown — distribute daily targets across meals realistically
            MealBreakdownItems.Clear();

            // Account for protein shakes first, then distribute remainder to food meals
            var foodCalories = targetCalories;
            var foodProteinG = proteinG;
            var foodCarbsG = carbsG;
            var foodFatG = fatG;

            if (data.UsesProteinShakes && data.ShakesPerDay > 0)
            {
                var shakeProtein = data.ShakesPerDay * data.ProteinPerShakeG;
                var shakeCarbs = data.ShakesPerDay * 3;   // ~3g carbs per shake
                var shakeFat = data.ShakesPerDay * 1;     // ~1g fat per shake
                var shakeCalories = shakeProtein * 4 + shakeCarbs * 4 + shakeFat * 9;

                MealBreakdownItems.Add(new MealCalorieItem
                {
                    MealName = $"Protein Shakes ({data.ShakesPerDay}x)",
                    Calories = shakeCalories,
                    ProteinG = shakeProtein,
                    CarbsG = shakeCarbs,
                    FatG = shakeFat
                });

                foodCalories = Math.Max(0, targetCalories - shakeCalories);
                foodProteinG = Math.Max(0, proteinG - shakeProtein);
                foodCarbsG = Math.Max(0, carbsG - shakeCarbs);
                foodFatG = Math.Max(0, fatG - shakeFat);
            }

            var mealDistribution = GetMealDistribution(data.MealsPerDay);
            foreach (var (mealName, fraction) in mealDistribution)
            {
                MealBreakdownItems.Add(new MealCalorieItem
                {
                    MealName = mealName,
                    Calories = (int)(foodCalories * fraction),
                    ProteinG = (int)(foodProteinG * fraction),
                    CarbsG = (int)(foodCarbsG * fraction),
                    FatG = (int)(foodFatG * fraction)
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        IsBusy = true;
        try
        {
            await _coordinator.CompleteAssessmentAsync();
        }
        catch (Exception ex)
        {
            // Stop silent failures — log to file AND surface to user
            try
            {
                var path = System.IO.Path.Combine(FileSystem.AppDataDirectory, "complete_error.txt");
                System.IO.File.WriteAllText(path,
                    $"@ {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Type: {ex.GetType().FullName}\n" +
                    $"Message: {ex.Message}\n\n" +
                    $"Stack:\n{ex.StackTrace}\n\n" +
                    (ex.InnerException == null ? "" :
                        $"Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\n" +
                        $"{ex.InnerException.StackTrace}"));
            }
            catch { /* ignore */ }

            await Shell.Current.DisplayAlert(
                "Couldn't complete assessment",
                $"{ex.GetType().Name}: {ex.Message}\n\n" +
                "Tap OK and tell the developer — this dialog replaces a previously-silent failure.",
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }

    private static double CalculateBodyFatNavy(
        double waistCm, double hipsCm, double neckCm, double heightCm, Gender gender)
    {
        if (waistCm <= neckCm || heightCm <= 0) return 0;

        double bodyFat;
        if (gender == Gender.Male)
        {
            var diff = waistCm - neckCm;
            if (diff <= 0) return 0;
            bodyFat = 86.010 * Math.Log10(diff) - 70.041 * Math.Log10(heightCm) + 36.76;
        }
        else
        {
            var sum = waistCm + hipsCm - neckCm;
            if (sum <= 0) return 0;
            bodyFat = 163.205 * Math.Log10(sum) - 97.684 * Math.Log10(heightCm) - 78.387;
        }

        return Math.Round(Math.Max(0, Math.Min(bodyFat, 60)), 1);
    }

    private static string SplitCamelCase(string input) =>
        Regex.Replace(input, @"(?<=[a-z])([A-Z])", " $1");

    /// <summary>
    /// Returns (MealName, Fraction) pairs that distribute daily calories realistically.
    /// Main meals get larger portions; snacks get smaller portions.
    /// </summary>
    private static List<(string MealName, double Fraction)> GetMealDistribution(int mealsPerDay) => mealsPerDay switch
    {
        1 => [("Meal", 1.0)],
        2 => [("Lunch", 0.45), ("Dinner", 0.55)],
        3 => [("Breakfast", 0.30), ("Lunch", 0.35), ("Dinner", 0.35)],
        4 => [("Breakfast", 0.25), ("Lunch", 0.30), ("Snack", 0.15), ("Dinner", 0.30)],
        5 => [("Breakfast", 0.25), ("Morning Snack", 0.10), ("Lunch", 0.25), ("Afternoon Snack", 0.10), ("Dinner", 0.30)],
        6 => [("Breakfast", 0.20), ("Morning Snack", 0.10), ("Lunch", 0.25), ("Afternoon Snack", 0.10), ("Dinner", 0.25), ("Evening Snack", 0.10)],
        _ => [("Breakfast", 0.30), ("Lunch", 0.35), ("Dinner", 0.35)]
    };
}

public class MealCalorieItem
{
    public string MealName { get; init; } = string.Empty;
    public int Calories { get; init; }
    public int ProteinG { get; init; }
    public int CarbsG { get; init; }
    public int FatG { get; init; }
    public string CaloriesDisplay => $"{Calories} kcal";
    public string MacrosDisplay => $"{ProteinG}g P  |  {CarbsG}g C  |  {FatG}g F";
}

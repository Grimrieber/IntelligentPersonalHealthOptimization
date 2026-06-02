using System.Text.Json;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class NutritionAssessmentCoordinator : INutritionAssessmentCoordinator
{
    private readonly IDatabaseService _databaseService;
    private readonly IUserService _userService;
    private readonly INutritionService _nutritionService;

    private static readonly string[] StepTitles =
    [
        "Nutrition Assessment",
        "Body Measurements",
        "Diet Plan",
        "Eating Patterns",
        "Motivation & Barriers",
        "Nutrition Goals",
        "Assessment Results"
    ];

    private static readonly string[] StepRoutes =
    [
        RouteConstants.NutriAssessIntro,
        RouteConstants.NutriAssessBodyComp,
        RouteConstants.NutriAssessDietPlan,
        RouteConstants.NutriAssessDietaryHabits,
        RouteConstants.NutriAssessBehavior,
        RouteConstants.NutriAssessGoals,
        RouteConstants.NutriAssessResult
    ];

    public NutritionAssessmentCoordinator(
        IDatabaseService databaseService,
        IUserService userService,
        INutritionService nutritionService)
    {
        _databaseService = databaseService;
        _userService = userService;
        _nutritionService = nutritionService;
    }

    public int CurrentStep { get; private set; }
    public int TotalSteps => StepTitles.Length;
    public string StepTitle => StepTitles[CurrentStep];
    public double ProgressPercentage => (CurrentStep + 1.0) / TotalSteps * 100;
    public bool CanGoNext => CurrentStep < TotalSteps - 1;
    public bool CanGoPrevious => CurrentStep > 0;
    public NutritionAssessmentData Data { get; private set; } = new();

    public async Task GoNextAsync()
    {
        if (!CanGoNext) return;
        CurrentStep++;
        await Shell.Current.GoToAsync(StepRoutes[CurrentStep]);
    }

    public async Task GoPreviousAsync()
    {
        if (!CanGoPrevious) return;
        CurrentStep--;
        await Shell.Current.GoToAsync("..");
    }

    public async Task InitializeAsync()
    {
        CurrentStep = 0;

        var user = await _userService.GetCurrentUserAsync();
        if (user == null)
        {
            Data = new NutritionAssessmentData();
            return;
        }

        // Load the most recent assessment to pre-populate all fields
        var previous = (await _databaseService.QueryAsync<NutritionAssessment>(
            "SELECT * FROM NutritionAssessment WHERE UserId = ? ORDER BY AssessmentDate DESC LIMIT 1",
            user.Id)).FirstOrDefault();

        if (previous == null)
        {
            Data = new NutritionAssessmentData();
            return;
        }

        // Pre-populate from the last completed assessment so users only change what's different.
        // Run JSON deserialization on a background thread to keep the UI responsive.
        Data = await Task.Run(() => new NutritionAssessmentData
        {
            // Body Composition
            NeckCm = previous.NeckCm,
            ChestCm = previous.ChestCm,
            WaistCm = previous.WaistCm,
            HipsCm = previous.HipsCm,
            ThighCm = previous.ThighCm,
            CalfCm = previous.CalfCm,
            BicepCm = previous.BicepCm,

            // Diet Plan
            SelectedDietType = previous.SelectedDietType,
            SelectedAllergies = DeserializeJson<List<FoodAllergy>>(previous.SelectedAllergiesJson) ?? [],
            FoodsToAvoid = DeserializeJson<List<string>>(previous.FoodsToAvoidJson) ?? [],
            MealsPerDay = previous.MealsPerDay > 0 ? previous.MealsPerDay : 3,
            UsesProteinShakes = previous.UsesProteinShakes,
            ShakesPerDay = previous.ShakesPerDay > 0 ? previous.ShakesPerDay : 1,
            ProteinPerShakeG = previous.ProteinPerShakeG > 0 ? previous.ProteinPerShakeG : 30,

            // Food Frequency
            FoodFrequencyResponses = DeserializeJson<List<FoodFrequencyResponse>>(previous.FoodFrequencyJson) ?? [],

            // Eating Patterns
            SelectedEatingPatterns = DeserializeJson<List<EatingPattern>>(previous.EatingPatternsJson) ?? [],
            SelectedEatingBehaviors = DeserializeJson<List<EatingBehavior>>(previous.EatingBehaviorsJson) ?? [],

            // Motivation & Barriers
            ConfidenceLevel = previous.ConfidenceLevel > 0 ? previous.ConfidenceLevel : 5,
            SelectedMotivations = DeserializeJson<List<NutritionMotivation>>(previous.MotivationsJson) ?? [],
            SelectedChallenges = DeserializeJson<List<NutritionChallenge>>(previous.ChallengesJson) ?? [],

            // Goals
            PrimaryGoal = previous.PrimaryGoal,
            SelectedFocusAreas = DeserializeJson<List<NutritionFocusArea>>(previous.FocusAreasJson) ?? [],
            SelectedTimeline = previous.SelectedTimeline,
            TargetWeightKg = previous.TargetWeightKg,

            // Planned Exercise
            WorkoutsPerWeek = previous.WorkoutsPerWeek,
            AvgWorkoutMinutes = previous.AvgWorkoutMinutes > 0 ? previous.AvgWorkoutMinutes : 45
        });

        // Pull water glasses from the live profile (not stored on assessment entity)
        var profile = await _nutritionService.GetNutritionProfileAsync(user.Id);
        if (profile != null)
            Data.DailyWaterGlasses = profile.DailyWaterGlasses;
    }

    private static T? DeserializeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }

    public async Task CompleteAssessmentAsync()
    {
        var user = await _userService.GetCurrentUserAsync();
        if (user == null) return;

        // Calculate body fat % using US Navy formula
        double bodyFatPercent = 0;
        if (Data.WaistCm > 0 && Data.NeckCm > 0)
        {
            bodyFatPercent = CalculateBodyFatNavy(
                Data.WaistCm, Data.HipsCm, Data.NeckCm,
                user.HeightCm, user.Gender);
        }

        // Calculate readiness score from confidence + motivation vs challenges
        var readinessScore = CalculateReadinessScore();

        // Calculate macro targets driven by assessment goals + diet type
        var bmr = _nutritionService.CalculateBMR(user);
        var tdee = _nutritionService.CalculateTDEE(bmr, user.ActivityLevel);
        var (targetCalories, proteinG, carbsG, fatG) = _nutritionService.CalculateAssessmentTargets(
            tdee, Data.PrimaryGoal, Data.SelectedDietType, user.Gender,
            user.WeightKg, Data.TargetWeightKg, Data.SelectedTimeline,
            Data.WorkoutsPerWeek, Data.AvgWorkoutMinutes,
            Data.SelectedFocusAreas, Data.ConfidenceLevel, readinessScore);

        // Create and save the assessment record
        var assessment = new NutritionAssessment
        {
            UserId = user.Id,
            AssessmentDate = DateTime.UtcNow,

            // Body Composition
            NeckCm = Data.NeckCm,
            ChestCm = Data.ChestCm,
            WaistCm = Data.WaistCm,
            HipsCm = Data.HipsCm,
            ThighCm = Data.ThighCm,
            CalfCm = Data.CalfCm,
            BicepCm = Data.BicepCm,
            CalculatedBodyFatPercent = bodyFatPercent,

            // Diet Plan
            SelectedDietType = Data.SelectedDietType,
            SelectedAllergiesJson = JsonSerializer.Serialize(Data.SelectedAllergies),
            FoodsToAvoidJson = JsonSerializer.Serialize(Data.FoodsToAvoid),
            MealsPerDay = Data.MealsPerDay,
            UsesProteinShakes = Data.UsesProteinShakes,
            ShakesPerDay = Data.ShakesPerDay,
            ProteinPerShakeG = Data.ProteinPerShakeG,
            MealPrepMode = Data.MealPrepMode,
            MealPrepDays = Data.MealPrepDays,

            // Food Frequency
            FoodFrequencyJson = JsonSerializer.Serialize(Data.FoodFrequencyResponses),

            // Eating Patterns
            EatingPatternsJson = JsonSerializer.Serialize(Data.SelectedEatingPatterns),
            EatingBehaviorsJson = JsonSerializer.Serialize(Data.SelectedEatingBehaviors),

            // Motivation & Barriers
            ConfidenceLevel = Data.ConfidenceLevel,
            MotivationsJson = JsonSerializer.Serialize(Data.SelectedMotivations),
            ChallengesJson = JsonSerializer.Serialize(Data.SelectedChallenges),
            ReadinessScore = readinessScore,

            // Goals
            PrimaryGoal = Data.PrimaryGoal,
            FocusAreasJson = JsonSerializer.Serialize(Data.SelectedFocusAreas),
            SelectedTimeline = Data.SelectedTimeline,
            TargetWeightKg = Data.TargetWeightKg,

            // Planned Exercise
            WorkoutsPerWeek = Data.WorkoutsPerWeek,
            AvgWorkoutMinutes = Data.AvgWorkoutMinutes,

            // Calculated targets
            RecommendedCalories = targetCalories,
            RecommendedProteinG = proteinG,
            RecommendedCarbsG = carbsG,
            RecommendedFatG = fatG
        };

        await _databaseService.InsertAsync(assessment);

        // Update or create NutritionProfile with latest targets and diet preferences
        var profile = await _nutritionService.GetNutritionProfileAsync(user.Id);
        // Serialize assessment data for profile storage
        var foodsToAvoidStr = Data.FoodsToAvoid.Count > 0
            ? string.Join(",", Data.FoodsToAvoid) : string.Empty;
        var eatingPatternsJson = JsonSerializer.Serialize(Data.SelectedEatingPatterns);
        // NOTE: focus areas, confidence, and readiness live on the NutritionAssessment
        // (written above). Do NOT mirror them into NutritionProfile — those columns are
        // deprecated. See project_goals_consolidation Step 2.

        if (profile == null)
        {
            // No profile exists (onboarding may have been incomplete) — create one from assessment data
            profile = await _nutritionService.CreateNutritionProfileAsync(
                user.Id, Data.SelectedDietType, Data.MealsPerDay,
                string.Join(",", Data.SelectedAllergies), string.Empty, string.Empty,
                Data.DailyWaterGlasses, string.Empty, string.Empty, 0,
                bmr, tdee, targetCalories, proteinG, carbsG, fatG);
            profile.FoodsToAvoid = foodsToAvoidStr;
            profile.EatingPatternsJson = eatingPatternsJson;
            profile.UsesProteinShakes = Data.UsesProteinShakes;
            profile.ShakesPerDay = Data.ShakesPerDay;
            profile.ProteinPerShakeG = Data.ProteinPerShakeG;
            profile.MealPrepMode = Data.MealPrepMode;
            profile.MealPrepDays = Data.MealPrepDays;
            await _databaseService.UpdateAsync(profile);
        }
        else
        {
            profile.TargetCalories = targetCalories;
            profile.TargetProteinG = proteinG;
            profile.TargetCarbsG = carbsG;
            profile.TargetFatG = fatG;
            profile.BMR = bmr;
            profile.TDEE = tdee;
            profile.DietType = Data.SelectedDietType;
            profile.MealsPerDay = Data.MealsPerDay;
            profile.Allergies = string.Join(",", Data.SelectedAllergies);
            profile.FoodsToAvoid = foodsToAvoidStr;
            profile.EatingPatternsJson = eatingPatternsJson;
            profile.DailyWaterGlasses = Data.DailyWaterGlasses;
            profile.UsesProteinShakes = Data.UsesProteinShakes;
            profile.ShakesPerDay = Data.ShakesPerDay;
            profile.ProteinPerShakeG = Data.ProteinPerShakeG;
            profile.MealPrepMode = Data.MealPrepMode;
            profile.MealPrepDays = Data.MealPrepDays;
            profile.UpdatedAt = DateTime.UtcNow;
            await _databaseService.UpdateAsync(profile);
        }

        // Generate the meal plan from the recipe database.
        // Falls back to template-based if recipe generation fails.
        try
        {
            await _nutritionService.GenerateRecipeMealPlanAsync(user.Id, profile.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Recipe meal plan failed: {ex.Message}");
            // Fallback to template-based generation
            try
            {
                await _nutritionService.GenerateMealPlanAsync(user.Id, profile.Id);
            }
            catch (Exception ex2)
            {
                System.Diagnostics.Debug.WriteLine($"Template meal plan also failed: {ex2.Message}");
                await Shell.Current.DisplayAlert("Meal Plan",
                    "Your assessment was saved but the meal plan could not be generated. " +
                    "You can regenerate it from the Meal Plan page.", "OK");
            }
        }

        // Navigate back to Nutrition tab
        await Shell.Current.GoToAsync($"//{RouteConstants.Nutrition}");
    }

    private double CalculateReadinessScore()
    {
        // Confidence contributes 50% (0-10 → 0-5)
        var confidenceComponent = Data.ConfidenceLevel / 10.0 * 5.0;

        // Motivation vs Challenges ratio contributes 50%
        var totalMotivations = Data.SelectedMotivations.Count;
        var totalChallenges = Data.SelectedChallenges.Count;
        var total = totalMotivations + totalChallenges;

        double balanceComponent;
        if (total > 0)
        {
            var ratio = (double)totalMotivations / total;
            balanceComponent = ratio * 10.0 / 10.0 * 5.0;
        }
        else
        {
            balanceComponent = 2.5; // neutral if nothing selected
        }

        return Math.Round(confidenceComponent + balanceComponent, 1);
    }

    /// <summary>
    /// US Navy body fat estimation formula.
    /// Male:   BF% = 86.010 × log10(waist - neck) - 70.041 × log10(height) + 36.76
    /// Female: BF% = 163.205 × log10(waist + hip - neck) - 97.684 × log10(height) - 78.387
    /// </summary>
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
}

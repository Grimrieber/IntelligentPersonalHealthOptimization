using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public partial class NutritionService : INutritionService
{
    private readonly IDatabaseService _databaseService;
    private readonly IFoodService _foodService;
    private readonly IMealPlanRecipeService _mealPlanRecipeService;

    public NutritionService(IDatabaseService databaseService, IFoodService foodService,
        IMealPlanRecipeService mealPlanRecipeService)
    {
        _databaseService = databaseService;
        _foodService = foodService;
        _mealPlanRecipeService = mealPlanRecipeService;
    }

    public double CalculateBMR(User user)
    {
        var age = DateTime.Today.Year - user.DateOfBirth.Year;
        if (user.DateOfBirth.Date > DateTime.Today.AddYears(-age))
            age--;

        // Mifflin-St Jeor equation
        var bmr = 10 * user.WeightKg + 6.25 * user.HeightCm - 5 * age;
        return user.Gender == Gender.Male ? bmr + 5 : bmr - 161;
    }

    public double CalculateTDEE(double bmr, ActivityLevel activityLevel)
    {
        var factor = activityLevel switch
        {
            ActivityLevel.Sedentary => 1.2,
            ActivityLevel.LightlyActive => 1.375,
            ActivityLevel.ModeratelyActive => 1.55,
            ActivityLevel.VeryActive => 1.725,
            ActivityLevel.ExtremelyActive => 1.9,
            _ => 1.55
        };
        return bmr * factor;
    }

    /// <summary>
    /// The single source of truth for how a <see cref="FitnessGoal"/> shifts daily calories
    /// relative to TDEE when there is no nutrition assessment. Defined once so the
    /// onboarding / settings / no-assessment paths can't drift apart.
    /// </summary>
    private static double FitnessGoalCalorieAdjustment(FitnessGoal fitnessGoal) => fitnessGoal switch
    {
        FitnessGoal.WeightLoss => -500,
        FitnessGoal.MuscleBuilding => 300,
        _ => 0
    };

    public (int proteinG, int carbsG, int fatG) CalculateMacroTargets(double tdee, FitnessGoal fitnessGoal)
    {
        var targetCalories = tdee + FitnessGoalCalorieAdjustment(fitnessGoal);

        double proteinPct, carbsPct, fatPct;
        switch (fitnessGoal)
        {
            case FitnessGoal.WeightLoss:
                proteinPct = 0.40; carbsPct = 0.30; fatPct = 0.30;
                break;
            case FitnessGoal.MuscleBuilding:
                proteinPct = 0.30; carbsPct = 0.45; fatPct = 0.25;
                break;
            case FitnessGoal.Endurance:
                proteinPct = 0.20; carbsPct = 0.55; fatPct = 0.25;
                break;
            default:
                proteinPct = 0.30; carbsPct = 0.40; fatPct = 0.30;
                break;
        }

        var proteinG = (int)(targetCalories * proteinPct / 4); // 4 cal/g
        var carbsG = (int)(targetCalories * carbsPct / 4);     // 4 cal/g
        var fatG = (int)(targetCalories * fatPct / 9);         // 9 cal/g

        return (proteinG, carbsG, fatG);
    }

    public (int proteinG, int carbsG, int fatG) CalculateMacroTargets(double tdee, FitnessGoal fitnessGoal, DietType dietType)
    {
        var targetCalories = tdee + FitnessGoalCalorieAdjustment(fitnessGoal);

        // Diet-specific macro splits override fitness goal defaults
        double proteinPct, carbsPct, fatPct;
        switch (dietType)
        {
            case DietType.Keto:
                proteinPct = 0.25; carbsPct = 0.05; fatPct = 0.70;
                break;
            case DietType.Paleo:
                proteinPct = 0.30; carbsPct = 0.25; fatPct = 0.45;
                break;
            case DietType.Mediterranean:
                proteinPct = 0.20; carbsPct = 0.45; fatPct = 0.35;
                break;
            default:
                // For Standard, Vegetarian, Vegan, Pescatarian, GlutenFree, DairyFree, Halal, Kosher
                // Use fitness-goal-based splits
                return CalculateMacroTargets(tdee, fitnessGoal);
        }

        var proteinG = (int)(targetCalories * proteinPct / 4);
        var carbsG = (int)(targetCalories * carbsPct / 4);
        var fatG = (int)(targetCalories * fatPct / 9);

        return (proteinG, carbsG, fatG);
    }

    /// <summary>
    /// THE single entry point for computing a user's nutrition targets (BMR/TDEE + macros).
    /// Every place that writes <see cref="NutritionProfile"/> targets — onboarding, the
    /// assessment, Settings, Add-Progress — must call this so they can never drift.
    ///
    /// When <paramref name="assessment"/> is provided, ALL of its goal inputs (goal, diet,
    /// target weight, timeline, workouts, focus areas, confidence, readiness) are read here
    /// so callers can't accidentally omit them (which previously produced different macros
    /// in Settings / Add-Progress than the assessment itself). When it is null (a freshly
    /// onboarded user with no assessment yet) the simpler FitnessGoal-based path is used.
    /// </summary>
    public NutritionTargets ComputeTargetsForUser(User user, NutritionAssessment? assessment)
    {
        var bmr = CalculateBMR(user);
        var tdee = CalculateTDEE(bmr, user.ActivityLevel);

        if (assessment != null)
        {
            var (calories, proteinG, carbsG, fatG) = CalculateAssessmentTargets(
                tdee, assessment.PrimaryGoal, assessment.SelectedDietType, user.Gender,
                user.WeightKg, assessment.TargetWeightKg, assessment.SelectedTimeline,
                assessment.WorkoutsPerWeek, assessment.AvgWorkoutMinutes,
                DeserializeFocusAreas(assessment.FocusAreasJson),
                assessment.ConfidenceLevel, assessment.ReadinessScore);
            return new NutritionTargets(bmr, tdee, calories, proteinG, carbsG, fatG);
        }
        else
        {
            var (proteinG, carbsG, fatG) = CalculateMacroTargets(tdee, user.FitnessGoal);
            var calories = (int)(tdee + FitnessGoalCalorieAdjustment(user.FitnessGoal));
            return new NutritionTargets(bmr, tdee, calories, proteinG, carbsG, fatG);
        }
    }

    // One-shot marker for the goals-consolidation Step 4 backfill. Self-gated by a file
    // so it runs exactly once per install. New marker (do NOT reuse the recipe/profile
    // one-shot markers — those reset other data).
    private const string TargetReconcileMarker = "nutrition_target_reconcile_2026_06_01_v1.done";

    /// <summary>
    /// One-shot, non-destructive backfill (goals consolidation Step 4). Installs created
    /// before the shared <see cref="ComputeTargetsForUser"/> entry point may have saved
    /// NutritionProfile macro targets that drifted — Settings / Add-Progress used to call
    /// the calculator without the assessment's focus/confidence/readiness inputs, producing
    /// different macros than the assessment itself. Recompute the saved profile's targets
    /// through the single shared entry point so every screen agrees. Self-gated; runs once.
    /// </summary>
    public async Task ReconcileSavedTargetsAsync(User user)
    {
        var markerPath = Path.Combine(FileSystem.AppDataDirectory, TargetReconcileMarker);
        if (File.Exists(markerPath))
            return;

        try
        {
            var profile = await GetNutritionProfileAsync(user.Id);
            if (profile != null)
            {
                // Drives off the latest assessment (or the FitnessGoal path when none) —
                // identical to what Settings/Add-Progress/onboarding now compute.
                var assessment = await GetLatestAssessmentAsync(user.Id);
                var t = ComputeTargetsForUser(user, assessment);

                profile.BMR = t.Bmr;
                profile.TDEE = t.Tdee;
                profile.TargetCalories = t.Calories;
                profile.TargetProteinG = t.ProteinG;
                profile.TargetCarbsG = t.CarbsG;
                profile.TargetFatG = t.FatG;
                profile.UpdatedAt = DateTime.UtcNow;
                await UpdateNutritionProfileAsync(profile);
            }

            // Write the marker even when there is no profile yet — nothing to reconcile,
            // and any profile created later goes through ComputeTargetsForUser anyway.
            File.WriteAllText(markerPath, $"applied {DateTime.UtcNow:O}");
        }
        catch
        {
            // Best-effort — don't disrupt dashboard load. Marker stays unwritten so it
            // retries on the next launch.
        }
    }

    private static List<NutritionFocusArea> DeserializeFocusAreas(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return System.Text.Json.JsonSerializer.Deserialize<List<NutritionFocusArea>>(json) ?? new(); }
        catch { return new(); }
    }

    public (int calories, int proteinG, int carbsG, int fatG) CalculateAssessmentTargets(
        double tdee, PrimaryNutritionGoal goal, DietType dietType, Gender gender,
        double currentWeightKg = 0, double targetWeightKg = 0, GoalTimeline timeline = GoalTimeline.TwelveWeeks,
        int workoutsPerWeek = 0, int avgWorkoutMinutes = 0,
        List<NutritionFocusArea>? focusAreas = null, int confidenceLevel = 5,
        double readinessScore = 5.0)
    {
        // Exercise data (workoutsPerWeek, avgWorkoutMinutes) is NOT added to TDEE here.
        // The user's ActivityLevel multiplier (Sedentary 1.2 → Extremely Active 1.9)
        // already accounts for their overall activity including exercise.
        // Adding exercise calories separately would double-count and inflate targets.

        // Default calorie adjustment when no target weight is set.
        // Each goal defines a sensible default direction and magnitude.
        var goalDefault = goal switch
        {
            PrimaryNutritionGoal.LoseWeight =>                -500,
            PrimaryNutritionGoal.LoseBodyFat =>               -500,
            PrimaryNutritionGoal.BodyRecomposition =>         -250,
            PrimaryNutritionGoal.PrepareForCompetition =>     -400,
            PrimaryNutritionGoal.BuildLeanMuscle =>            250,
            PrimaryNutritionGoal.AggressiveMuscleGain =>       500,
            PrimaryNutritionGoal.ImproveAthletePerformance =>  200,
            PrimaryNutritionGoal.ImproveEndurance =>           150,
            PrimaryNutritionGoal.IncreaseEnergy =>             150,
            PrimaryNutritionGoal.MaintainWeight =>               0,
            _ =>                                                 0,
        };

        // Timeline intensity: shorter = more aggressive, longer = more gradual.
        var timelineIntensity = timeline switch
        {
            GoalTimeline.SixWeeks => 1.3,
            GoalTimeline.EightWeeks => 1.2,
            GoalTimeline.TwelveWeeks => 1.0,
            GoalTimeline.SixMonths => 0.7,
            _ => 1.0
        };

        double dailyAdjustment;

        if (targetWeightKg > 0 && currentWeightKg > 0 && Math.Abs(targetWeightKg - currentWeightKg) > 0.5)
        {
            // Target weight drives the calorie adjustment — this is the user's explicit intent.
            // If they want to lose weight while building muscle, the deficit is respected.
            var weightDiffKg = targetWeightKg - currentWeightKg;
            var timelineDays = timeline switch
            {
                GoalTimeline.SixWeeks => 42,
                GoalTimeline.EightWeeks => 56,
                GoalTimeline.TwelveWeeks => 84,
                GoalTimeline.SixMonths => 182,
                _ => 84
            };

            // 1 kg of body weight change ~ 7,700 kcal
            var weightBasedAdjust = weightDiffKg * 7700.0 / timelineDays;

            // Clamp to safe limits: max 1000 cal deficit, max 750 cal surplus
            dailyAdjustment = Math.Clamp(weightBasedAdjust * timelineIntensity, -1000, 750);
        }
        else
        {
            // No meaningful target weight difference — use the goal's default
            dailyAdjustment = goalDefault * timelineIntensity;
        }

        // Readiness-based adjustment: if confidence is low (readiness < 4), reduce the
        // aggressiveness of the deficit/surplus to set more achievable targets.
        // This prevents setting unrealistic goals for users who aren't ready.
        if (readinessScore < 4.0)
        {
            // Scale deficit/surplus toward maintenance (reduce by 30-50%)
            var readinessDampen = 0.5 + (readinessScore / 4.0) * 0.5; // 0.5 at score 0, 1.0 at score 4
            dailyAdjustment *= readinessDampen;
        }

        // Gender-aware safety floor: 1500 kcal for males, 1200 kcal for females.
        // These are the medically recommended minimum daily intakes.
        var calorieFloor = gender == Gender.Male ? 1500 : 1200;
        var targetCalories = Math.Max(tdee + dailyAdjustment, calorieFloor);

        // Macro split: specific diet types override goal-based ratios
        double proteinPct, carbsPct, fatPct;
        switch (dietType)
        {
            case DietType.Keto:
                proteinPct = 0.25; carbsPct = 0.05; fatPct = 0.70;
                break;
            case DietType.Paleo:
                proteinPct = 0.30; carbsPct = 0.25; fatPct = 0.45;
                break;
            case DietType.Mediterranean:
                proteinPct = 0.20; carbsPct = 0.45; fatPct = 0.35;
                break;
            default:
                // Goal-based macro ratios for Standard and other diet types
                (proteinPct, carbsPct, fatPct) = goal switch
                {
                    PrimaryNutritionGoal.LoseWeight =>             (0.35, 0.35, 0.30), // Moderate protein
                    PrimaryNutritionGoal.LoseBodyFat =>            (0.40, 0.30, 0.30), // High protein, preserve muscle
                    PrimaryNutritionGoal.BodyRecomposition =>      (0.40, 0.35, 0.25), // High protein, moderate carbs
                    PrimaryNutritionGoal.PrepareForCompetition =>  (0.45, 0.25, 0.30), // Very high protein, low carb
                    PrimaryNutritionGoal.BuildLeanMuscle =>        (0.30, 0.45, 0.25), // High carbs for training
                    PrimaryNutritionGoal.AggressiveMuscleGain =>   (0.25, 0.50, 0.25), // Very high carbs, bulk
                    PrimaryNutritionGoal.ImproveAthletePerformance => (0.25, 0.50, 0.25), // Performance carbs
                    PrimaryNutritionGoal.ImproveEndurance =>       (0.20, 0.55, 0.25), // Endurance carb-heavy
                    PrimaryNutritionGoal.IncreaseEnergy =>         (0.25, 0.50, 0.25), // Balanced energy
                    _ =>                                           (0.30, 0.40, 0.30), // Maintain, Health, Condition
                };
                break;
        }

        // Focus area adjustments: shift macro ratios based on user's selected focus areas.
        // These are small nudges (2-5%) that respect the user's priorities.
        if (focusAreas is { Count: > 0 })
        {
            if (focusAreas.Contains(NutritionFocusArea.IncreaseProtein))
            {
                proteinPct += 0.05;
                carbsPct -= 0.025;
                fatPct -= 0.025;
            }
            if (focusAreas.Contains(NutritionFocusArea.IncludeHealthyFats))
            {
                fatPct += 0.03;
                carbsPct -= 0.03;
            }
            if (focusAreas.Contains(NutritionFocusArea.EatMoreFiber) || focusAreas.Contains(NutritionFocusArea.EatMoreWholeGrains))
            {
                carbsPct += 0.03;
                fatPct -= 0.03;
            }
            if (focusAreas.Contains(NutritionFocusArea.ReduceSugar) || focusAreas.Contains(NutritionFocusArea.ReduceProcessedFood))
            {
                // Shift slightly toward protein and fat, away from simple carbs
                carbsPct -= 0.03;
                proteinPct += 0.015;
                fatPct += 0.015;
            }

            // Ensure ratios still sum to 1.0 and stay in valid ranges
            var totalPct = proteinPct + carbsPct + fatPct;
            proteinPct /= totalPct;
            carbsPct /= totalPct;
            fatPct /= totalPct;

            // Clamp to reasonable ranges
            proteinPct = Math.Clamp(proteinPct, 0.15, 0.50);
            carbsPct = Math.Clamp(carbsPct, 0.05, 0.60);
            fatPct = Math.Clamp(fatPct, 0.15, 0.70);
        }

        // Water goal influence: if DrinkMoreWater is a focus area, the profile's
        // DailyWaterGlasses target is already set. No calorie adjustment needed
        // but it feeds into the dashboard hydration tracking.

        var proteinG = (int)(targetCalories * proteinPct / 4);
        var fatG = (int)(targetCalories * fatPct / 9);

        // Body-weight-based protein floor for muscle-focused goals.
        // %-of-calorie protein splits under-protein large/lean athletes — this
        // override enforces sports-nutrition g/kg minimums (ISSN guidelines).
        var proteinFloorGPerKg = goal switch
        {
            PrimaryNutritionGoal.PrepareForCompetition    => 2.4, // peak cut, max LBM preservation
            PrimaryNutritionGoal.LoseBodyFat              => 2.0,
            PrimaryNutritionGoal.BodyRecomposition        => 2.0,
            PrimaryNutritionGoal.AggressiveMuscleGain     => 1.8,
            PrimaryNutritionGoal.BuildLeanMuscle          => 1.8,
            PrimaryNutritionGoal.ImproveAthletePerformance => 1.6,
            _ => 0.0,
        };

        if (proteinFloorGPerKg > 0 && currentWeightKg > 0)
        {
            var floorG = (int)Math.Round(proteinFloorGPerKg * currentWeightKg);
            if (floorG > proteinG)
                proteinG = floorG;
        }

        // Carbs = remaining calories after protein + fat. Keeps macro sum
        // aligned with targetCalories regardless of whether the protein floor
        // overrode the %-split.
        var remainingKcal = targetCalories - (proteinG * 4) - (fatG * 9);
        var carbsG = (int)Math.Max(0, remainingKcal / 4);

        return ((int)targetCalories, proteinG, carbsG, fatG);
    }

    public async Task<NutritionProfile> CreateNutritionProfileAsync(int userId, DietType dietType, int mealsPerDay,
        string allergies, string foodPreferences, string foodDislikes, int waterGlasses,
        string supplements, string alcoholFrequency, int caffeinePerDay,
        double bmr, double tdee, int targetCalories, int proteinG, int carbsG, int fatG)
    {
        var profile = new NutritionProfile
        {
            UserId = userId,
            DietType = dietType,
            MealsPerDay = mealsPerDay,
            Allergies = allergies,
            FoodPreferences = foodPreferences,
            FoodDislikes = foodDislikes,
            DailyWaterGlasses = waterGlasses,
            SupplementUse = supplements,
            AlcoholFrequency = alcoholFrequency,
            CaffeinePerDay = caffeinePerDay,
            BMR = bmr,
            TDEE = tdee,
            TargetCalories = targetCalories,
            TargetProteinG = proteinG,
            TargetCarbsG = carbsG,
            TargetFatG = fatG
        };

        await _databaseService.InsertAsync(profile);
        return profile;
    }

    public async Task<NutritionProfile?> GetNutritionProfileAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<NutritionProfile>()
            .Where(p => p.UserId == userId)
            .FirstOrDefaultAsync();
    }

    public async Task<NutritionProfile> UpdateNutritionProfileAsync(NutritionProfile profile)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        await _databaseService.UpdateAsync(profile);
        return profile;
    }

    public async Task<NutritionAssessment?> GetLatestAssessmentAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<NutritionAssessment>()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.AssessmentDate)
            .FirstOrDefaultAsync();
    }

    // ===== Meal Plan Generation (Template-Based) =====

    public async Task<MealPlan> GenerateMealPlanAsync(int userId, int nutritionProfileId)
    {
        var profile = await _databaseService.GetByIdAsync<NutritionProfile>(nutritionProfileId);
        if (profile == null) throw new InvalidOperationException("Nutrition profile not found");

        // Deactivate existing plans
        var db = await _databaseService.GetConnectionAsync();
        var existingPlans = await db.Table<MealPlan>()
            .Where(p => p.UserId == userId && p.IsActive)
            .ToListAsync();
        foreach (var p in existingPlans)
        {
            p.IsActive = false;
            await _databaseService.UpdateAsync(p);
        }

        var plan = new MealPlan
        {
            UserId = userId,
            NutritionProfileId = nutritionProfileId,
            PlanName = $"{FormatDietTypeName(profile.DietType)} Meal Plan",
            TargetCalories = profile.TargetCalories,
            TargetProteinG = profile.TargetProteinG,
            TargetCarbsG = profile.TargetCarbsG,
            TargetFatG = profile.TargetFatG,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await _databaseService.InsertAsync(plan);

        // Load all foods and build lookups (by name and by ID)
        var allFoods = await db.Table<Food>().Where(f => f.IsActive).ToListAsync();
        var foodLookup = new Dictionary<string, Food>(StringComparer.OrdinalIgnoreCase);
        var foodById = new Dictionary<int, Food>();
        foreach (var f in allFoods)
        {
            foodLookup.TryAdd(f.Name, f);
            foodById.TryAdd(f.Id, f);
        }

        // Get compatible meal template categories for this diet
        var compatibleCategories = GetCompatibleCategories(profile.DietType);
        var mealTypes = GetMealTypesForCount(profile.MealsPerDay, profile);

        // Calculate shake macros and subtract from daily targets for food meals.
        // Shakes are added separately, so food meals should only target the remainder.
        var foodTargetCalories = (double)profile.TargetCalories;
        var foodTargetProtein = (double)profile.TargetProteinG;
        var foodTargetCarbs = (double)profile.TargetCarbsG;
        var foodTargetFat = (double)profile.TargetFatG;
        Food? wheyFood = null;
        double shakeServingG = 0;

        if (profile.UsesProteinShakes && profile.ShakesPerDay > 0 && profile.ProteinPerShakeG > 0)
        {
            if (foodLookup.TryGetValue("Whey Protein Powder", out wheyFood))
            {
                shakeServingG = Math.Round(profile.ProteinPerShakeG / (wheyFood.ProteinPer100g / 100.0));
                var shakeFactor = shakeServingG / 100.0;
                var shakeCalEach = wheyFood.CaloriesPer100g * shakeFactor;
                var shakePEach = wheyFood.ProteinPer100g * shakeFactor;
                var shakeCEach = wheyFood.CarbsPer100g * shakeFactor;
                var shakeFEach = wheyFood.FatPer100g * shakeFactor;

                foodTargetCalories -= profile.ShakesPerDay * shakeCalEach;
                foodTargetProtein -= profile.ShakesPerDay * shakePEach;
                foodTargetCarbs -= profile.ShakesPerDay * shakeCEach;
                foodTargetFat -= profile.ShakesPerDay * shakeFEach;

                // Ensure food targets don't go negative
                foodTargetCalories = Math.Max(foodTargetCalories, 500);
                foodTargetProtein = Math.Max(foodTargetProtein, 20);
                foodTargetCarbs = Math.Max(foodTargetCarbs, 20);
                foodTargetFat = Math.Max(foodTargetFat, 10);
            }
        }

        // Calculate per-slot calorie and macro targets (snacks get 60% of main meal portions)
        var mainCount = mealTypes.Count(mt => mt is not (MealType.MorningSnack or MealType.AfternoonSnack or MealType.EveningSnack));
        var snackCount = mealTypes.Count - mainCount;
        var mainCalBase = snackCount > 0
            ? foodTargetCalories / (mainCount + snackCount * 0.6)
            : foodTargetCalories / Math.Max(mainCount, 1);
        var snackCalBase = mainCalBase * 0.6;

        // Per-slot macro targets (distribute proportionally like calories)
        var mainFraction = mainCalBase / Math.Max(1, foodTargetCalories);
        var snackFraction = snackCalBase / Math.Max(1, foodTargetCalories);
        var mainProtein = foodTargetProtein * mainFraction;
        var mainCarbs = foodTargetCarbs * mainFraction;
        var mainFat = foodTargetFat * mainFraction;
        var snackProtein = foodTargetProtein * snackFraction;
        var snackCarbs = foodTargetCarbs * snackFraction;
        var snackFat = foodTargetFat * snackFraction;

        // Pre-shuffle templates per meal type for weekly variety
        var rng = new Random();
        var templatesByMealType = new Dictionary<MealType, List<MealTemplate>>();
        foreach (var mt in mealTypes)
        {
            var candidates = _mealTemplates
                .Where(t => t.ApplicableMealTypes.Contains(mt) &&
                            compatibleCategories.Contains(t.Category))
                .OrderBy(_ => rng.Next())
                .ToList();
            templatesByMealType[mt] = candidates;
        }

        var dayNames = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        for (int d = 0; d < 7; d++)
        {
            var mealDay = new MealPlanDay
            {
                MealPlanId = plan.Id,
                DayNumber = d + 1,
                DayName = dayNames[d]
            };
            await _databaseService.InsertAsync(mealDay);

            // Collect all food items for the day before saving (so correction pass can adjust)
            var dayFoodItems = new List<MealPlanItem>();

            foreach (var mealType in mealTypes)
            {
                var isSnack = mealType is MealType.MorningSnack or MealType.AfternoonSnack or MealType.EveningSnack;
                var targetCal = isSnack ? snackCalBase : mainCalBase;

                var templates = templatesByMealType[mealType];
                if (templates.Count == 0) continue;

                var template = templates[d % templates.Count];
                var slotProtein = isSnack ? snackProtein : mainProtein;
                var slotCarbs = isSnack ? snackCarbs : mainCarbs;
                var slotFat = isSnack ? snackFat : mainFat;
                var mealItems = BuildMealFromTemplate(template, mealType, targetCal, foodLookup, profile,
                    slotProtein, slotCarbs, slotFat);

                dayFoodItems.AddRange(mealItems);

                // If target not met (>200 cal deficit), add a side dish for main meals
                var mealCalories = mealItems.Sum(i => i.Calories);
                var deficit = targetCal - mealCalories;
                if (deficit > 200 && !isSnack)
                {
                    var usedName = template.Name;
                    var sideTemplate = templates.FirstOrDefault(t => t.Name != usedName);
                    if (sideTemplate != null)
                    {
                        var remainingProtein = Math.Max(0, slotProtein - mealItems.Sum(i => i.ProteinG));
                        var remainingCarbs = Math.Max(0, slotCarbs - mealItems.Sum(i => i.CarbsG));
                        var remainingFat = Math.Max(0, slotFat - mealItems.Sum(i => i.FatG));
                        var sideItems = BuildMealFromTemplate(sideTemplate, mealType, deficit, foodLookup, profile,
                            remainingProtein, remainingCarbs, remainingFat);
                        dayFoodItems.AddRange(sideItems);
                    }
                }
            }

            // Run day-level macro correction pass on food items
            CorrectDayMacros(dayFoodItems, foodById, foodTargetProtein, foodTargetCarbs, foodTargetFat);

            // Save all food items to DB
            var dailyCalories = 0.0;
            int order = 0;
            foreach (var item in dayFoodItems)
            {
                item.MealPlanDayId = mealDay.Id;
                item.OrderIndex = order++;
                await _databaseService.InsertAsync(item);
                dailyCalories += item.Calories;
            }

            // Add protein shakes (these are fixed, not corrected)
            if (wheyFood != null && profile.UsesProteinShakes && profile.ShakesPerDay > 0)
            {
                var shakeSlots = GetProteinShakeSlots(profile.ShakesPerDay);
                var factor = shakeServingG / 100.0;
                int shakeOrder = 100;

                foreach (var slotType in shakeSlots)
                {
                    var shakeItem = new MealPlanItem
                    {
                        MealPlanDayId = mealDay.Id,
                        FoodId = wheyFood.Id,
                        MealType = slotType,
                        MealName = "Protein Shake",
                        ServingSizeG = shakeServingG,
                        Calories = Math.Round(wheyFood.CaloriesPer100g * factor, 1),
                        ProteinG = Math.Round(wheyFood.ProteinPer100g * factor, 1),
                        CarbsG = Math.Round(wheyFood.CarbsPer100g * factor, 1),
                        FatG = Math.Round(wheyFood.FatPer100g * factor, 1),
                        OrderIndex = shakeOrder++
                    };
                    await _databaseService.InsertAsync(shakeItem);
                    dailyCalories += shakeItem.Calories;
                }
            }

            mealDay.TotalCalories = (int)dailyCalories;
            await _databaseService.UpdateAsync(mealDay);
        }

        return plan;
    }

    public async Task<MealPlan?> GetActiveMealPlanAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<MealPlan>()
            .Where(p => p.UserId == userId && p.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<List<MealPlanDay>> GetMealPlanDaysAsync(int mealPlanId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<MealPlanDay>()
            .Where(d => d.MealPlanId == mealPlanId)
            .OrderBy(d => d.DayNumber)
            .ToListAsync();
    }

    public async Task<List<MealPlanItem>> GetMealItemsAsync(int mealPlanDayId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<MealPlanItem>()
            .Where(i => i.MealPlanDayId == mealPlanDayId)
            .OrderBy(i => i.MealType)
            .ThenBy(i => i.OrderIndex)
            .ToListAsync();
    }

    public async Task<MealPlan> RegenerateMealPlanAsync(int userId, int nutritionProfileId)
    {
        return await GenerateMealPlanAsync(userId, nutritionProfileId);
    }

    public List<string> GetAvailableMealNames(MealType mealType, DietType dietType)
    {
        var categories = GetCompatibleCategories(dietType);
        return _mealTemplates
            .Where(t => t.ApplicableMealTypes.Contains(mealType) && categories.Contains(t.Category))
            .Select(t => t.Name)
            .ToList();
    }

    public List<MealTemplateInfo> GetAvailableMealsWithInfo(MealType mealType, DietType dietType, int targetCaloriesForSlot = 0)
    {
        var categories = GetCompatibleCategories(dietType);
        return _mealTemplates
            .Where(t => t.ApplicableMealTypes.Contains(mealType) && categories.Contains(t.Category))
            .Select(t => new MealTemplateInfo
            {
                Name = t.Name,
                Category = t.Category.ToString(),
                Ingredients = string.Join(", ", t.Components.Select(c => c.FoodName)),
                EstimatedCalories = targetCaloriesForSlot > 0
                    ? EstimateScaledCalories(t, targetCaloriesForSlot)
                    : EstimateTemplateCalories(t)
            })
            .OrderBy(m => m.Name)
            .ToList();
    }

    private static int EstimateScaledCalories(MealTemplate template, int targetCalories)
    {
        var baseCal = EstimateTemplateCalories(template);
        if (baseCal <= 0) return 0;
        var scaleFactor = Math.Clamp((double)targetCalories / baseCal, 0.5, 5.0);
        return (int)(baseCal * scaleFactor);
    }

    private static int EstimateTemplateCalories(MealTemplate template)
    {
        // Rough calorie estimate based on known food calorie densities at default serving
        // This avoids needing DB access; just a reference for the UI
        var knownCalories = new Dictionary<string, (double calPer100g, double defaultServing)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chicken Breast (cooked)"] = (165, 120), ["Chicken Thigh (cooked)"] = (209, 115),
            ["Turkey Breast (cooked)"] = (135, 120), ["Ground Turkey (cooked)"] = (170, 115),
            ["Ground Beef 90% Lean"] = (176, 115), ["Ground Beef 80% Lean"] = (254, 115),
            ["Sirloin Steak (cooked)"] = (206, 170), ["Pork Tenderloin (cooked)"] = (143, 115),
            ["Pork Chop (cooked)"] = (231, 140), ["Salmon (cooked)"] = (208, 170),
            ["Tuna (canned in water)"] = (116, 85), ["Cod (cooked)"] = (105, 170),
            ["Tilapia (cooked)"] = (128, 115), ["Shrimp (cooked)"] = (99, 85),
            ["Whole Egg"] = (155, 50), ["Egg Whites"] = (52, 33),
            ["Tofu (firm)"] = (144, 125), ["Tempeh"] = (192, 85),
            ["Edamame (shelled)"] = (121, 80), ["Black Beans (cooked)"] = (132, 130),
            ["Lentils (cooked)"] = (116, 100), ["Chickpeas (cooked)"] = (164, 125),
            ["Kidney Beans (cooked)"] = (127, 130), ["Greek Yogurt (plain, nonfat)"] = (59, 170),
            ["Cottage Cheese (low-fat)"] = (72, 113), ["Whey Protein Powder"] = (370, 30),
            ["White Rice (cooked)"] = (130, 158), ["Brown Rice (cooked)"] = (123, 158),
            ["Quinoa (cooked)"] = (120, 185), ["Oats (dry)"] = (389, 40),
            ["Pasta (cooked)"] = (131, 140), ["Whole Wheat Pasta (cooked)"] = (124, 140),
            ["Whole Wheat Bread"] = (247, 28), ["White Bread"] = (265, 25),
            ["Corn Tortilla"] = (218, 26), ["Flour Tortilla"] = (312, 45),
            ["Bagel (plain)"] = (257, 105), ["English Muffin"] = (227, 57),
            ["Couscous (cooked)"] = (112, 157), ["Granola"] = (471, 40),
            ["Rice Cakes"] = (392, 9), ["Sweet Potato (baked)"] = (90, 130),
            ["Potato (baked)"] = (93, 150),
            ["Broccoli"] = (34, 91), ["Spinach (raw)"] = (23, 30),
            ["Kale (raw)"] = (35, 21), ["Romaine Lettuce"] = (17, 47),
            ["Tomato"] = (18, 125), ["Cucumber"] = (15, 150),
            ["Bell Pepper (red)"] = (31, 120), ["Carrots"] = (41, 61),
            ["Zucchini"] = (17, 180), ["Cauliflower"] = (25, 107),
            ["Green Beans"] = (31, 100), ["Asparagus"] = (20, 90),
            ["Mushrooms (white)"] = (22, 70), ["Corn (sweet)"] = (86, 90),
            ["Peas (green)"] = (81, 80), ["Brussels Sprouts"] = (43, 88),
            ["Cabbage"] = (25, 89), ["Avocado"] = (160, 68),
            ["Eggplant"] = (25, 82), ["Beets"] = (43, 82),
            ["Olive Oil"] = (884, 14), ["Coconut Oil"] = (862, 14),
            ["Almonds"] = (579, 28), ["Walnuts"] = (654, 28),
            ["Cashews"] = (553, 28), ["Peanuts"] = (567, 28),
            ["Peanut Butter"] = (588, 32), ["Almond Butter"] = (614, 32),
            ["Chia Seeds"] = (486, 28), ["Flaxseeds"] = (534, 10),
            ["Sunflower Seeds"] = (584, 28), ["Pumpkin Seeds"] = (559, 28),
            ["Dark Chocolate (70%)"] = (598, 28), ["Tahini"] = (595, 15),
            ["Banana"] = (89, 118), ["Apple"] = (52, 182),
            ["Orange"] = (47, 131), ["Strawberries"] = (32, 152),
            ["Blueberries"] = (57, 148), ["Raspberries"] = (52, 123),
            ["Mango"] = (60, 165), ["Pineapple"] = (50, 165),
            ["Grapes"] = (69, 92), ["Watermelon"] = (30, 152),
            ["Peach"] = (39, 150), ["Pear"] = (57, 166),
            ["Kiwi"] = (61, 69), ["Dates (Medjool)"] = (277, 24),
            ["Honey"] = (304, 21), ["Maple Syrup"] = (260, 20),
            ["Whole Milk"] = (61, 244), ["2% Milk"] = (50, 244),
            ["Almond Milk (unsweetened)"] = (15, 240), ["Oat Milk"] = (48, 240),
            ["Soy Milk"] = (33, 240), ["Cheddar Cheese"] = (403, 28),
            ["Mozzarella Cheese"] = (280, 28), ["Parmesan Cheese"] = (431, 10),
            ["Feta Cheese"] = (264, 28), ["Cream Cheese"] = (342, 28),
            ["Butter"] = (717, 14), ["Yogurt (plain, whole milk)"] = (61, 245),
            ["Protein Bar"] = (380, 60), ["Granola Bar"] = (471, 40),
            ["Hummus"] = (166, 30), ["Trail Mix"] = (462, 40),
            ["Beef Jerky"] = (410, 28), ["Mixed Nuts"] = (607, 28),
            ["Crackers (whole wheat)"] = (443, 28), ["Salsa"] = (36, 30),
            ["Guacamole"] = (160, 30), ["Popcorn (air-popped)"] = (387, 28),
            ["Tortilla Chips"] = (489, 28), ["Rice Cakes"] = (392, 9),
            ["Chocolate Milk"] = (83, 240), ["Coconut Water"] = (19, 240),
        };

        double total = 0;
        foreach (var (foodName, multiplier) in template.Components)
        {
            if (knownCalories.TryGetValue(foodName, out var info))
                total += info.calPer100g * info.defaultServing * multiplier / 100.0;
        }
        return (int)total;
    }

    public async Task SwapMealAsync(int mealPlanDayId, MealType mealType, string mealTemplateName, int nutritionProfileId)
    {
        var profile = await _databaseService.GetByIdAsync<NutritionProfile>(nutritionProfileId);
        if (profile == null) return;

        var db = await _databaseService.GetConnectionAsync();

        // Delete existing food items for this meal type on this day,
        // but preserve protein shakes (they have their own card)
        var allDayItems = await db.Table<MealPlanItem>()
            .Where(i => i.MealPlanDayId == mealPlanDayId)
            .ToListAsync();
        var existingItems = allDayItems
            .Where(i => i.MealType == mealType && i.MealName != "Protein Shake")
            .ToList();
        foreach (var item in existingItems)
            await db.DeleteAsync(item);

        // Find the template
        var categories = GetCompatibleCategories(profile.DietType);
        var template = _mealTemplates.FirstOrDefault(t =>
            t.Name == mealTemplateName &&
            t.ApplicableMealTypes.Contains(mealType) &&
            categories.Contains(t.Category));
        if (template == null) return;

        // Load foods
        var allFoods = await db.Table<Food>().Where(f => f.IsActive).ToListAsync();
        var foodLookup = new Dictionary<string, Food>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in allFoods)
            foodLookup.TryAdd(f.Name, f);

        // Subtract shake macros from daily targets for food-only calculation
        var foodTargetCal = (double)profile.TargetCalories;
        var foodTargetP = (double)profile.TargetProteinG;
        var foodTargetC = (double)profile.TargetCarbsG;
        var foodTargetF = (double)profile.TargetFatG;

        if (profile.UsesProteinShakes && profile.ShakesPerDay > 0 && profile.ProteinPerShakeG > 0)
        {
            if (foodLookup.TryGetValue("Whey Protein Powder", out var wheyFood))
            {
                var servingG = Math.Round(profile.ProteinPerShakeG / (wheyFood.ProteinPer100g / 100.0));
                var fac = servingG / 100.0;
                foodTargetCal -= profile.ShakesPerDay * wheyFood.CaloriesPer100g * fac;
                foodTargetP -= profile.ShakesPerDay * wheyFood.ProteinPer100g * fac;
                foodTargetC -= profile.ShakesPerDay * wheyFood.CarbsPer100g * fac;
                foodTargetF -= profile.ShakesPerDay * wheyFood.FatPer100g * fac;
                foodTargetCal = Math.Max(foodTargetCal, 500);
                foodTargetP = Math.Max(foodTargetP, 20);
                foodTargetC = Math.Max(foodTargetC, 20);
                foodTargetF = Math.Max(foodTargetF, 10);
            }
        }

        // Calculate calorie target for this slot using food-only targets
        var mealTypes = GetMealTypesForCount(profile.MealsPerDay, profile);
        var isSnack = mealType is MealType.MorningSnack or MealType.AfternoonSnack or MealType.EveningSnack;
        var mainCount = mealTypes.Count(mt => mt is not (MealType.MorningSnack or MealType.AfternoonSnack or MealType.EveningSnack));
        var snackCount = mealTypes.Count - mainCount;
        var mainCalBase = snackCount > 0
            ? foodTargetCal / (mainCount + snackCount * 0.6)
            : foodTargetCal / Math.Max(mainCount, 1);
        var targetCal = isSnack ? mainCalBase * 0.6 : mainCalBase;
        var slotFraction = targetCal / Math.Max(1, foodTargetCal);
        var slotProtein = foodTargetP * slotFraction;
        var slotCarbs = foodTargetC * slotFraction;
        var slotFat = foodTargetF * slotFraction;

        var mealItems = BuildMealFromTemplate(template, mealType, targetCal, foodLookup, profile,
            slotProtein, slotCarbs, slotFat);

        int order = 0;
        foreach (var item in mealItems)
        {
            item.MealPlanDayId = mealPlanDayId;
            item.OrderIndex = order++;
            await _databaseService.InsertAsync(item);
        }

        // Recalculate day total
        var updatedDayItems = await db.Table<MealPlanItem>()
            .Where(i => i.MealPlanDayId == mealPlanDayId)
            .ToListAsync();
        var day = await _databaseService.GetByIdAsync<MealPlanDay>(mealPlanDayId);
        if (day != null)
        {
            day.TotalCalories = (int)updatedDayItems.Sum(i => i.Calories);
            await _databaseService.UpdateAsync(day);
        }
    }

    private static MealType[] GetProteinShakeSlots(int shakesPerDay) => shakesPerDay switch
    {
        1 => [MealType.AfternoonSnack],
        2 => [MealType.MorningSnack, MealType.AfternoonSnack],
        3 => [MealType.MorningSnack, MealType.AfternoonSnack, MealType.EveningSnack],
        4 => [MealType.MorningSnack, MealType.AfternoonSnack, MealType.PreWorkout, MealType.EveningSnack],
        5 => [MealType.MorningSnack, MealType.AfternoonSnack, MealType.PreWorkout, MealType.PostWorkout, MealType.EveningSnack],
        _ => [MealType.AfternoonSnack]
    };

    // ===== Recipe-Based Meal Plan Generation =====

    public async Task<MealPlan> GenerateRecipeMealPlanAsync(int userId, int nutritionProfileId)
    {
        var profile = await _databaseService.GetByIdAsync<NutritionProfile>(nutritionProfileId);
        if (profile == null) throw new InvalidOperationException("Nutrition profile not found");

        // Deactivate existing plans
        var db = await _databaseService.GetConnectionAsync();
        var existingPlans = await db.Table<MealPlan>()
            .Where(p => p.UserId == userId && p.IsActive)
            .ToListAsync();
        foreach (var p in existingPlans)
        {
            p.IsActive = false;
            await _databaseService.UpdateAsync(p);
        }

        // Build/refresh the recipe pool from API (or use cache)
        var pool = await _mealPlanRecipeService.BuildRecipePoolAsync(userId);
        pool = _mealPlanRecipeService.FilterByDiet(pool, profile);

        if (pool.Count < 5)
            throw new InvalidOperationException(
                $"Not enough recipes with nutrition data ({pool.Count} found). " +
                "Please ensure the recipe API is running and recipes have nutrition information.");

        // Determine plan duration from latest assessment timeline
        var assessment = await GetLatestAssessmentAsync(userId);
        var planDays = assessment?.SelectedTimeline switch
        {
            GoalTimeline.SixWeeks => 42,
            GoalTimeline.EightWeeks => 56,
            GoalTimeline.TwelveWeeks => 84,
            GoalTimeline.SixMonths => 182,
            _ => 84
        };

        var plan = new MealPlan
        {
            UserId = userId,
            NutritionProfileId = nutritionProfileId,
            PlanName = $"{FormatDietTypeName(profile.DietType)} Recipe Plan",
            TargetCalories = profile.TargetCalories,
            TargetProteinG = profile.TargetProteinG,
            TargetCarbsG = profile.TargetCarbsG,
            TargetFatG = profile.TargetFatG,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(planDays)
        };
        await _databaseService.InsertAsync(plan);

        // Get meal types respecting eating patterns
        var mealTypes = GetMealTypesForCount(profile.MealsPerDay, profile);

        // Calculate per-slot calorie targets
        var foodTargetCal = (double)profile.TargetCalories;
        Food? wheyFood = null;
        double shakeServingG = 0;

        // Subtract shake calories from food targets
        if (profile.UsesProteinShakes && profile.ShakesPerDay > 0 && profile.ProteinPerShakeG > 0)
        {
            var allFoods = await db.Table<Food>().Where(f => f.IsActive).ToListAsync();
            wheyFood = allFoods.FirstOrDefault(f =>
                f.Name.Equals("Whey Protein Powder", StringComparison.OrdinalIgnoreCase));
            if (wheyFood != null)
            {
                shakeServingG = Math.Round(profile.ProteinPerShakeG / (wheyFood.ProteinPer100g / 100.0));
                var factor = shakeServingG / 100.0;
                foodTargetCal -= profile.ShakesPerDay * wheyFood.CaloriesPer100g * factor;
                foodTargetCal = Math.Max(foodTargetCal, 500);
            }
        }

        var mainCount = mealTypes.Count(mt => mt is not (MealType.MorningSnack or MealType.AfternoonSnack or MealType.EveningSnack));
        var snackCount = mealTypes.Count - mainCount;
        var mainCalTarget = snackCount > 0
            ? foodTargetCal / (mainCount + snackCount * 0.6)
            : foodTargetCal / Math.Max(mainCount, 1);
        var snackCalTarget = mainCalTarget * 0.6;

        // Calculate target macro percentages from profile targets
        var totalTargetCal = Math.Max(profile.TargetCalories, 1);
        var targetProteinPct = (profile.TargetProteinG * 4.0) / totalTargetCal;
        var targetCarbsPct = (profile.TargetCarbsG * 4.0) / totalTargetCal;
        var targetFatPct = (profile.TargetFatG * 9.0) / totalTargetCal;

        var startDate = DateTime.UtcNow.Date;
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        // The meal-type set repeats every day, so filter the pool once per distinct
        // meal type instead of re-scanning all ~3,500 recipes for every one of the
        // ~250 slots in the plan.
        var candidatesByMealType = mealTypes
            .Distinct()
            .ToDictionary(mt => mt, mt => _mealPlanRecipeService.FilterForMealType(pool, mt));

        // Meal prep: a cooked recipe covers `prepDays` consecutive days (batch
        // cooking). 1 = a new recipe every day (max variety). Pre-pick the recipe
        // for each block of days, per meal type, so a meal stays the same across
        // its block instead of changing daily.
        var prepDays = profile.MealPrepMode ? Math.Max(1, profile.MealPrepDays) : 1;
        var blockCount = (planDays + prepDays - 1) / prepDays;
        var blocksPerWeek = Math.Max(1, 7 / prepDays); // refresh the variety pool ~weekly

        var distinctMealTypes = mealTypes.Distinct().ToList();
        var recipeSchedule = distinctMealTypes.ToDictionary(mt => mt, _ => new List<SavedRecipe?>(blockCount));

        // Build block-by-block (not meal-type-by-meal-type) with a used-set shared
        // ACROSS meal slots within each block, so different slots don't land on the
        // same recipe — e.g. lunch and dinner draw from the same pool, so without
        // this they'd both pick the single best calorie match. usedRecent carries
        // picks across blocks for week-to-week variety and resets each week.
        var usedRecent = new HashSet<int>();
        for (int b = 0; b < blockCount; b++)
        {
            if (b % blocksPerWeek == 0) usedRecent.Clear(); // allow repeats across weeks
            var usedThisBlock = new HashSet<int>(usedRecent);

            foreach (var mealType in distinctMealTypes)
            {
                var isSnack = mealType is MealType.MorningSnack or MealType.AfternoonSnack or MealType.EveningSnack;
                var slotTarget = isSnack ? snackCalTarget : mainCalTarget;
                var picked = _mealPlanRecipeService.SelectBestMatch(candidatesByMealType[mealType], slotTarget,
                    usedThisBlock, targetProteinPct, targetCarbsPct, targetFatPct);
                if (picked != null)
                {
                    usedThisBlock.Add(picked.Id);
                    usedRecent.Add(picked.Id);
                }
                recipeSchedule[mealType].Add(picked);
            }
        }

        // All inputs (recipe pool, profile, meal types) are already in memory, so
        // build the whole multi-week plan inside ONE transaction with synchronous
        // inserts. Previously each day + meal was a separate awaited insert — ~400+
        // individually-fsync'd writes on the encrypted DB, which took minutes for a
        // 12-week plan. One transaction commits in ~1 second.
        await db.RunInTransactionAsync(conn =>
        {
            for (int d = 0; d < planDays; d++)
            {
                var date = startDate.AddDays(d);
                var weekNum = d / 7 + 1;
                var blockIndex = d / prepDays;

                var mealDay = new MealPlanDay
                {
                    MealPlanId = plan.Id,
                    DayNumber = d + 1,
                    DayName = $"Week {weekNum} - {dayNames[(int)date.DayOfWeek]}",
                    Date = date
                };
                conn.Insert(mealDay);

                double dailyCalories = 0;
                int order = 0;

                foreach (var mealType in mealTypes)
                {
                    var isSnack = mealType is MealType.MorningSnack or MealType.AfternoonSnack or MealType.EveningSnack;
                    var targetCal = isSnack ? snackCalTarget : mainCalTarget;

                    // Same recipe for every day in the current block (meal prep).
                    var recipe = recipeSchedule[mealType][blockIndex];

                    if (recipe != null)
                    {
                        var recipeCal = recipe.CaloriesPerServing ?? 1;
                        var servings = recipeCal > 0 ? Math.Round(targetCal / recipeCal, 1) : 1;
                        // Allow scaling below one serving so a recipe larger than the
                        // slot target is portioned down to hit the calories, instead
                        // of overshooting at a forced 1x. 0.5–5 servings, 0.1 precision.
                        servings = Math.Max(0.5, Math.Min(servings, 5));

                        var item = new MealPlanItem
                        {
                            MealPlanDayId = mealDay.Id,
                            SavedRecipeId = recipe.Id,
                            MealType = mealType,
                            MealName = recipe.RecipeName,
                            Servings = servings,
                            Calories = Math.Round(recipeCal * servings),
                            ProteinG = Math.Round((recipe.ProteinGrams ?? 0) * servings, 1),
                            CarbsG = Math.Round((recipe.CarbsGrams ?? 0) * servings, 1),
                            FatG = Math.Round((recipe.FatGrams ?? 0) * servings, 1),
                            OrderIndex = order++
                        };
                        conn.Insert(item);
                        dailyCalories += item.Calories;
                    }
                }

                // Add protein shakes
                if (wheyFood != null && profile.UsesProteinShakes && profile.ShakesPerDay > 0)
                {
                    var shakeSlots = GetProteinShakeSlots(profile.ShakesPerDay);
                    var factor = shakeServingG / 100.0;
                    int shakeOrder = 100;

                    foreach (var slotType in shakeSlots)
                    {
                        var shakeItem = new MealPlanItem
                        {
                            MealPlanDayId = mealDay.Id,
                            FoodId = wheyFood.Id,
                            MealType = slotType,
                            MealName = "Protein Shake",
                            ServingSizeG = shakeServingG,
                            Calories = Math.Round(wheyFood.CaloriesPer100g * factor, 1),
                            ProteinG = Math.Round(wheyFood.ProteinPer100g * factor, 1),
                            CarbsG = Math.Round(wheyFood.CarbsPer100g * factor, 1),
                            FatG = Math.Round(wheyFood.FatPer100g * factor, 1),
                            OrderIndex = shakeOrder++
                        };
                        conn.Insert(shakeItem);
                        dailyCalories += shakeItem.Calories;
                    }
                }

                mealDay.TotalCalories = (int)dailyCalories;
                conn.Update(mealDay);
            }
        });

        return plan;
    }

    public async Task<List<MealPlanItem>> GetTodaysMealPlanItemsAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        var activePlan = await db.Table<MealPlan>()
            .Where(p => p.UserId == userId && p.IsActive)
            .FirstOrDefaultAsync();

        if (activePlan == null) return [];

        var today = DateTime.UtcNow.Date;

        // Try to find day by exact date first
        var day = await db.Table<MealPlanDay>()
            .Where(d => d.MealPlanId == activePlan.Id && d.Date == today)
            .FirstOrDefaultAsync();

        // Fallback: calculate day number from plan creation
        if (day == null)
        {
            var daysSinceStart = (int)(today - activePlan.CreatedAt.Date).TotalDays;
            if (daysSinceStart >= 0)
            {
                var dayNumber = daysSinceStart % 7 + 1; // cycle through 7 days for old plans
                day = await db.Table<MealPlanDay>()
                    .Where(d => d.MealPlanId == activePlan.Id && d.DayNumber == dayNumber)
                    .FirstOrDefaultAsync();
            }
        }

        if (day == null) return [];

        return await db.Table<MealPlanItem>()
            .Where(i => i.MealPlanDayId == day.Id)
            .OrderBy(i => i.MealType)
            .ThenBy(i => i.OrderIndex)
            .ToListAsync();
    }

    // ===== Meal Template System =====
    // Template types, helpers, and data are in the partial class file:
    // NutritionService.MealTemplates.cs
}

namespace IntelligentPersonalHealthOptimization.Models.Enums;

public enum EatingPattern
{
    EatBreakfastRegularly,
    SkipBreakfast,
    EatLunchRegularly,
    SkipLunch,
    EatDinnerRegularly,
    EatLateAtNight,
    EatAtConsistentTimes,
    GrazeAllDay
}

public enum NutritionMotivation
{
    MoreEnergy,
    WeightLoss,
    BuildMuscle,
    BetterHealth,
    AthleticPerformance,
    ManageHealthCondition,
    BetterSleep,
    ImprovedMood,
    FamilyRoleModel,
    BodyConfidence
}

public enum NutritionChallenge
{
    BusySchedule,
    LimitedCookingSkills,
    BudgetConstraints,
    Cravings,
    EmotionalEating,
    SocialPressure,
    LackOfSupport,
    ConfusionAboutDiet,
    InconsistentSchedule,
    HealthConditionLimits,
    LimitedFoodAccess,
    Motivation
}

// Only focus areas that actually adjust the macro split are kept (see
// NutritionService.CalculateAssessmentTargets). Explicit integer values preserve
// the original mapping so previously-stored FocusAreasJson still deserializes.
// Removed (cosmetic / untrackable): EatMoreVegetables(0), ControlPortions(2),
// DrinkMoreWater(5), ReduceSodium(7), ReduceFastFood(10), CookMoreAtHome(11),
// ConsistentMealSchedule(12), ReduceSnacking(13).
public enum NutritionFocusArea
{
    ReduceProcessedFood = 1,
    IncreaseProtein = 3,
    ReduceSugar = 4,
    EatMoreFiber = 6,
    EatMoreWholeGrains = 8,
    IncludeHealthyFats = 9
}

public enum PrimaryNutritionGoal
{
    LoseWeight,
    LoseBodyFat,
    BuildLeanMuscle,
    BodyRecomposition,
    AggressiveMuscleGain,
    MaintainWeight,
    ImproveOverallHealth,
    IncreaseEnergy,
    ImproveAthletePerformance,
    ImproveEndurance,
    ManageHealthCondition,
    PrepareForCompetition
}

public enum GoalTimeline
{
    SixWeeks,
    EightWeeks,
    TwelveWeeks,
    SixMonths
}

using CommunityToolkit.Mvvm.ComponentModel;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Models;

/// <summary>
/// Transient data holder for the nutrition assessment wizard.
/// All inputs are selection-based — no free-text fields.
/// </summary>
public class NutritionAssessmentData
{
    // Body Composition - Circumference (cm)
    public double NeckCm { get; set; }
    public double ChestCm { get; set; }
    public double WaistCm { get; set; }
    public double HipsCm { get; set; }
    public double ThighCm { get; set; }
    public double CalfCm { get; set; }
    public double BicepCm { get; set; }

    // Diet Plan & Preferences
    public DietType SelectedDietType { get; set; } = DietType.Standard;
    public List<FoodAllergy> SelectedAllergies { get; set; } = [];
    public List<string> FoodsToAvoid { get; set; } = [];
    public int MealsPerDay { get; set; } = 3;
    public bool UsesProteinShakes { get; set; }
    public int ShakesPerDay { get; set; } = 1;
    public int ProteinPerShakeG { get; set; } = 30;
    public int DailyWaterGlasses { get; set; } = 8;

    // Meal prep: when on, the plan cooks a few recipes and repeats each for
    // MealPrepDays consecutive days instead of a new recipe every meal.
    // On by default — cooking 21 different recipes a week is unrealistic.
    public bool MealPrepMode { get; set; } = true;
    public int MealPrepDays { get; set; } = 3;

    // Eating Patterns
    public List<EatingPattern> SelectedEatingPatterns { get; set; } = [];

    // Motivation & Barriers
    public int ConfidenceLevel { get; set; } = 5;
    public List<NutritionMotivation> SelectedMotivations { get; set; } = [];
    public List<NutritionChallenge> SelectedChallenges { get; set; } = [];

    // Nutrition Goals
    public PrimaryNutritionGoal PrimaryGoal { get; set; } = PrimaryNutritionGoal.ImproveOverallHealth;
    public List<NutritionFocusArea> SelectedFocusAreas { get; set; } = [];
    public GoalTimeline SelectedTimeline { get; set; } = GoalTimeline.TwelveWeeks;
    public double TargetWeightKg { get; set; }

    // Planned Exercise
    public int WorkoutsPerWeek { get; set; }
    public int AvgWorkoutMinutes { get; set; } = 45;
}

/// <summary>
/// Non-generic base for CheckableItem so XAML DataTemplates can use x:DataType
/// (generic types cannot be referenced as x:DataType in MAUI XAML).
/// </summary>
public partial class CheckableItemBase : ObservableObject
{
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isChecked;
}

/// <summary>
/// Reusable checkable item for checkbox-based selections in the assessment UI.
/// </summary>
public class CheckableItem<T> : CheckableItemBase where T : Enum
{
    public T Value { get; init; } = default!;
}

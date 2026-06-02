using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class NutritionAssessment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;

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

    [MaxLength(500)]
    public string SelectedAllergiesJson { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string FoodsToAvoidJson { get; set; } = string.Empty;

    public int MealsPerDay { get; set; } = 3;
    public bool UsesProteinShakes { get; set; }
    public int ShakesPerDay { get; set; }
    public int ProteinPerShakeG { get; set; }
    public bool MealPrepMode { get; set; }
    public int MealPrepDays { get; set; } = 3;

    // Eating Patterns (JSON)
    [MaxLength(1000)]
    public string EatingPatternsJson { get; set; } = string.Empty;

    // Motivation & Barriers
    public int ConfidenceLevel { get; set; }

    [MaxLength(1000)]
    public string MotivationsJson { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string ChallengesJson { get; set; } = string.Empty;

    public double ReadinessScore { get; set; }

    // Goals
    public PrimaryNutritionGoal PrimaryGoal { get; set; }

    [MaxLength(2000)]
    public string FocusAreasJson { get; set; } = string.Empty;

    public GoalTimeline SelectedTimeline { get; set; }
    public double TargetWeightKg { get; set; }

    // Planned Exercise
    public int WorkoutsPerWeek { get; set; }
    public int AvgWorkoutMinutes { get; set; }

    // Calculated targets
    public int RecommendedCalories { get; set; }
    public int RecommendedProteinG { get; set; }
    public int RecommendedCarbsG { get; set; }
    public int RecommendedFatG { get; set; }
}

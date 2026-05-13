using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class NutritionProfile
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public DietType DietType { get; set; }

    public int MealsPerDay { get; set; } = 3;

    [MaxLength(500)]
    public string Allergies { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string FoodPreferences { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string FoodDislikes { get; set; } = string.Empty;

    public int DailyWaterGlasses { get; set; } = 8;

    public bool UsesProteinShakes { get; set; }
    public int ShakesPerDay { get; set; }
    public int ProteinPerShakeG { get; set; }


    [MaxLength(500)]
    public string SupplementUse { get; set; } = string.Empty;

    [MaxLength(50)]
    public string AlcoholFrequency { get; set; } = "None";

    public int CaffeinePerDay { get; set; }

    [MaxLength(1000)]
    public string FoodsToAvoid { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string EatingPatternsJson { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string FocusAreasJson { get; set; } = string.Empty;

    public int ConfidenceLevel { get; set; }
    public double ReadinessScore { get; set; }

    public double BMR { get; set; }
    public double TDEE { get; set; }
    public int TargetCalories { get; set; }
    public int TargetProteinG { get; set; }
    public int TargetCarbsG { get; set; }
    public int TargetFatG { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class MealPlan
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    [Indexed]
    public int NutritionProfileId { get; set; }

    [MaxLength(200)]
    public string PlanName { get; set; } = string.Empty;

    public int TargetCalories { get; set; }
    public int TargetProteinG { get; set; }
    public int TargetCarbsG { get; set; }
    public int TargetFatG { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

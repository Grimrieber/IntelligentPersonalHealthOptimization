using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class MealPlanItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int MealPlanDayId { get; set; }

    [Indexed]
    public int FoodId { get; set; }

    [Indexed]
    public int SavedRecipeId { get; set; }

    public MealType MealType { get; set; }

    public double ServingSizeG { get; set; }
    public double Calories { get; set; }
    public double ProteinG { get; set; }
    public double CarbsG { get; set; }
    public double FatG { get; set; }

    public int OrderIndex { get; set; }

    public double Servings { get; set; } = 1;

    [MaxLength(200)]
    public string MealName { get; set; } = string.Empty;
}

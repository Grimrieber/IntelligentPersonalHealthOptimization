using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class MealPlanDay
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int MealPlanId { get; set; }

    public int DayNumber { get; set; }

    [MaxLength(50)]
    public string DayName { get; set; } = string.Empty;

    public int TotalCalories { get; set; }

    public DateTime? Date { get; set; }
}

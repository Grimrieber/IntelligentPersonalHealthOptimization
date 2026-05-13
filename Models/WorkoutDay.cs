using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("WorkoutDays")]
public class WorkoutDay
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WorkoutProgramId { get; set; }

    public int DayNumber { get; set; }

    [MaxLength(100)]
    public string DayName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Focus { get; set; } = string.Empty;
}

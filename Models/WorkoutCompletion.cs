using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class WorkoutCompletion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ScheduledWorkoutId { get; set; }

    public DateTime CompletedDate { get; set; }
    public int DurationMinutes { get; set; }
    public int Difficulty { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int? CaloriesBurned { get; set; }
}

using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

/// <summary>
/// Logs what the user actually did for a given set of an exercise on a workout day.
/// One row per set. Drives progress charts and 1RM re-estimation.
/// </summary>
[Table("ExercisePerformance")]
public class ExercisePerformanceEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    [Indexed]
    public int WorkoutDayId { get; set; }

    [Indexed]
    public int ExerciseId { get; set; }

    public int SetNumber { get; set; }

    /// <summary>Null for bodyweight exercises.</summary>
    public decimal? WeightKg { get; set; }

    public int RepsCompleted { get; set; }

    /// <summary>Optional 1-10 RPE if user wants to log it.</summary>
    public int? RpeOutOf10 { get; set; }

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}

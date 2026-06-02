using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("WorkoutExercises")]
public class WorkoutExercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WorkoutDayId { get; set; }

    [Indexed]
    public int ExerciseId { get; set; }

    public int OrderIndex { get; set; }

    public ExerciseCategory Category { get; set; }

    public int Sets { get; set; }

    public int RepsMin { get; set; }

    public int RepsMax { get; set; }

    [MaxLength(20)]
    public string Tempo { get; set; } = string.Empty;

    public int RestSeconds { get; set; }

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Engine-suggested working weight in kg. Null when no working max is mapped
    /// (e.g. bodyweight exercises, accessories without a derivation).
    /// </summary>
    public decimal? RecommendedWeightKg { get; set; }
}

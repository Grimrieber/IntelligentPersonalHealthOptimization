using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

/// <summary>
/// User's current working max for a benchmark lift. Drives weight recommendations
/// for prescribed exercises that map to this lift via name.
/// </summary>
[Table("WorkingWeights")]
public class WorkingWeight
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    /// <summary>Canonical key, e.g. "bench-press", "back-squat", "deadlift", "overhead-press", "barbell-row".</summary>
    [MaxLength(50), NotNull]
    public string LiftKey { get; set; } = string.Empty;

    public decimal WeightKg { get; set; }

    /// <summary>True if the user said "estimate" rather than entering a measured value.</summary>
    public bool IsEstimated { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

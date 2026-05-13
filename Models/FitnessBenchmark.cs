using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class FitnessBenchmark
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int AssessmentSessionId { get; set; }

    public int PushUpCount { get; set; }

    public int PlankHoldSeconds { get; set; }

    public int SquatCount { get; set; }

    [MaxLength(200)]
    public string CardioTestResult { get; set; } = string.Empty;

    public double? EstimatedVO2Max { get; set; }

    [MaxLength(50)]
    public string OverallFitnessLevel { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

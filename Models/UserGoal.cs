using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class UserGoal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public GoalCategory GoalCategory { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public double? TargetValue { get; set; }

    [MaxLength(50)]
    public string? TargetUnit { get; set; }

    public double? StartValue { get; set; }
    public double? CurrentValue { get; set; }

    public GoalTimeframe Timeframe { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

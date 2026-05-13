using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class GoalMilestone
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserGoalId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public double TargetValue { get; set; }

    public bool IsReached { get; set; }
    public DateTime? ReachedDate { get; set; }

    public int OrderIndex { get; set; }
}

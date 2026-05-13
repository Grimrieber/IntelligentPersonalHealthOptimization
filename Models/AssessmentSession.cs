using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("AssessmentSessions")]
public class AssessmentSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public DateTime AssessmentDate { get; set; }

    public bool IsComplete { get; set; }

    public int OverallMovementScore { get; set; }

    public DateTime CreatedAt { get; set; }
}

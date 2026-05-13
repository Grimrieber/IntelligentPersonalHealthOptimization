using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("AssessmentResults")]
public class AssessmentResult
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int AssessmentSessionId { get; set; }

    public AssessmentType AssessmentType { get; set; }

    [MaxLength(1000)]
    public string DetectedCompensations { get; set; } = string.Empty;

    public int Score { get; set; }

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

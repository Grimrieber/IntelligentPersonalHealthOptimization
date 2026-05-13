using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class FlexibilityAssessment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int AssessmentSessionId { get; set; }

    public FlexibilityTest TestType { get; set; }

    public int RangeOfMotionDegrees { get; set; }

    public bool IsNormal { get; set; }

    [MaxLength(20)]
    public string Side { get; set; } = "Bilateral";

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

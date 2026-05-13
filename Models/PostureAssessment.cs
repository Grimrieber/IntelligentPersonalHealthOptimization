using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class PostureAssessment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int AssessmentSessionId { get; set; }

    [MaxLength(1000)]
    public string DetectedDeviations { get; set; } = string.Empty;

    public int AnteriorScore { get; set; }
    public int LateralScore { get; set; }
    public int PosteriorScore { get; set; }

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

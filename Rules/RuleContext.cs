using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules;

public class RuleContext
{
    public User User { get; set; } = null!;
    public AssessmentSession? Session { get; set; }
    public List<AssessmentResult> Results { get; set; } = new();
    public List<MovementCompensation> AllCompensations { get; set; } = new();
    public int OverallMovementScore { get; set; }

    // Expanded context for enhanced prescription
    public TrainingProfile? TrainingProfile { get; set; }
    public FitnessBenchmark? FitnessBenchmark { get; set; }
    public List<PostureDeviation> PostureDeviations { get; set; } = new();
    public List<string> AvailableEquipment { get; set; } = new();
    public int DaysPerWeek { get; set; } = 3;
    public int SessionDurationMinutes { get; set; } = 60;
}

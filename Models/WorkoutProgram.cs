using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("WorkoutPrograms")]
public class WorkoutProgram
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    [Indexed]
    public int? AssessmentSessionId { get; set; }

    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    public ExercisePhase Phase { get; set; }

    public int DaysPerWeek { get; set; }

    public int DurationWeeks { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}

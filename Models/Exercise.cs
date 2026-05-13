using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("Exercises")]
public class Exercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(200), NotNull]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string FormCues { get; set; } = string.Empty;

    public ExerciseCategory Category { get; set; }

    public MuscleGroup PrimaryMuscle { get; set; }

    [MaxLength(500)]
    public string SecondaryMuscles { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Equipment { get; set; } = string.Empty;

    public int DifficultyLevel { get; set; }

    [MaxLength(500)]
    public string VideoUrl { get; set; } = string.Empty;

    [MaxLength(500)]
    public string CorrectsCompensations { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

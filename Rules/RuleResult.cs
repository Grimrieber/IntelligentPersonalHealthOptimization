using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules;

public class RuleResult
{
    public List<ExercisePrescription> CorrectiveExercises { get; set; } = new();
    public List<ExercisePrescription> WarmupExercises { get; set; } = new();
    public List<ExercisePrescription> MainExercises { get; set; } = new();
    public List<ExercisePrescription> CooldownExercises { get; set; } = new();
    public ExercisePhase RecommendedPhase { get; set; }
    public int DaysPerWeek { get; set; }
    public int DurationWeeks { get; set; }
}

public class ExercisePrescription
{
    public int ExerciseId { get; set; }
    public int Sets { get; set; }
    public int RepsMin { get; set; }
    public int RepsMax { get; set; }
    public string Tempo { get; set; } = string.Empty;
    public int RestSeconds { get; set; }
    public int OrderIndex { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int DayIndex { get; set; } // 0, 1, 2 for which workout day
}

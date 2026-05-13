using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules.PrescriptionRules;

public class MainProgramRules : IRuleSet
{
    private readonly List<Exercise> _exerciseLibrary;

    public MainProgramRules(List<Exercise> exerciseLibrary)
    {
        _exerciseLibrary = exerciseLibrary;
    }

    public void Evaluate(RuleContext context, RuleResult result)
    {
        // Determine training phase based on movement score
        result.RecommendedPhase = context.OverallMovementScore switch
        {
            <= 40 => ExercisePhase.Stabilization,
            <= 60 => ExercisePhase.MuscularEndurance,
            <= 80 => ExercisePhase.Hypertrophy,
            _ => ExercisePhase.Strength
        };

        result.DaysPerWeek = context.DaysPerWeek > 0 ? context.DaysPerWeek : AppConstants.DefaultDaysPerWeek;
        result.DurationWeeks = AppConstants.DefaultProgramDurationWeeks;

        var (sets, repsMin, repsMax, tempo, rest) = GetPhaseParameters(result.RecommendedPhase);

        int maxDifficulty = result.RecommendedPhase switch
        {
            ExercisePhase.Stabilization => 2,
            ExercisePhase.MuscularEndurance => 3,
            ExercisePhase.Hypertrophy => 4,
            _ => 5
        };

        // Build dynamic splits based on days per week
        var daySplits = GetDaySplits(result.DaysPerWeek);

        for (int dayIdx = 0; dayIdx < daySplits.Length; dayIdx++)
        {
            int order = 1;
            foreach (var muscle in daySplits[dayIdx].muscles)
            {
                var exercise = _exerciseLibrary
                    .Where(e => e.Category == ExerciseCategory.Main
                        && e.PrimaryMuscle == muscle
                        && e.DifficultyLevel <= maxDifficulty
                        && e.IsActive
                        && HasAvailableEquipment(e, context.AvailableEquipment))
                    .OrderByDescending(e => e.DifficultyLevel)
                    .FirstOrDefault();

                if (exercise != null)
                {
                    result.MainExercises.Add(new ExercisePrescription
                    {
                        ExerciseId = exercise.Id,
                        Sets = sets,
                        RepsMin = repsMin,
                        RepsMax = repsMax,
                        Tempo = tempo,
                        RestSeconds = rest,
                        OrderIndex = order++,
                        DayIndex = dayIdx
                    });
                }
            }
        }

        // Add cooldown stretches
        var cooldowns = _exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Cooldown)
            .Take(4)
            .ToList();

        int cooldownOrder = 1;
        foreach (var exercise in cooldowns)
        {
            result.CooldownExercises.Add(new ExercisePrescription
            {
                ExerciseId = exercise.Id,
                Sets = 1,
                RepsMin = 1,
                RepsMax = 1,
                Tempo = "Hold 30s",
                RestSeconds = 0,
                OrderIndex = cooldownOrder++,
                Notes = "Hold for 30 seconds per side"
            });
        }
    }

    private static bool HasAvailableEquipment(Exercise exercise, List<string> availableEquipment)
    {
        if (availableEquipment.Count == 0) return true;
        if (string.IsNullOrEmpty(exercise.Equipment)) return true;

        var exerciseEquip = exercise.Equipment.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLowerInvariant());

        return exerciseEquip.All(eq =>
            availableEquipment.Any(ae => ae.Trim().Equals(eq, StringComparison.OrdinalIgnoreCase))
            || eq == "bodyweight" || eq == "wall" || eq == "doorway");
    }

    private static (string name, MuscleGroup[] muscles)[] GetDaySplits(int daysPerWeek)
    {
        return daysPerWeek switch
        {
            2 =>
            [
                ("Upper Body", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.UpperBack, MuscleGroup.Triceps, MuscleGroup.Biceps, MuscleGroup.Core]),
                ("Lower Body", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves, MuscleGroup.Core])
            ],
            4 =>
            [
                ("Upper Push", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps]),
                ("Lower Body", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves]),
                ("Upper Pull", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps]),
                ("Core + Glutes", [MuscleGroup.Core, MuscleGroup.Glutes, MuscleGroup.HipFlexors])
            ],
            5 =>
            [
                ("Chest + Triceps", [MuscleGroup.Chest, MuscleGroup.Triceps]),
                ("Back + Biceps", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps]),
                ("Legs", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Calves]),
                ("Shoulders + Core", [MuscleGroup.Shoulders, MuscleGroup.Core, MuscleGroup.Obliques]),
                ("Glutes + Full Body", [MuscleGroup.Glutes, MuscleGroup.HipFlexors, MuscleGroup.Core])
            ],
            6 =>
            [
                ("Push", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps]),
                ("Pull", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps]),
                ("Legs", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Calves]),
                ("Push + Core", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Core]),
                ("Pull + Core", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Core]),
                ("Legs + Glutes", [MuscleGroup.Quadriceps, MuscleGroup.Glutes, MuscleGroup.Calves])
            ],
            _ =>
            [
                ("Push + Core", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps, MuscleGroup.Core]),
                ("Pull + Core", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps, MuscleGroup.Core]),
                ("Legs + Glutes", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves])
            ]
        };
    }

    private static (int sets, int repsMin, int repsMax, string tempo, int rest) GetPhaseParameters(ExercisePhase phase) => phase switch
    {
        ExercisePhase.Stabilization => (2, 12, 20, "4-2-1-0", 60),
        ExercisePhase.MuscularEndurance => (3, 12, 15, "2-0-2-0", 60),
        ExercisePhase.Hypertrophy => (3, 8, 12, "2-0-2-0", 90),
        ExercisePhase.Strength => (4, 4, 6, "2-0-1-0", 120),
        ExercisePhase.Power => (3, 1, 5, "X-0-X-0", 120),
        _ => (3, 10, 12, "2-0-2-0", 60)
    };
}

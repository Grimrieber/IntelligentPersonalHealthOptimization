using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules.PrescriptionRules;

public class WarmupSelectionRules : IRuleSet
{
    private readonly List<Exercise> _exerciseLibrary;

    public WarmupSelectionRules(List<Exercise> exerciseLibrary)
    {
        _exerciseLibrary = exerciseLibrary;
    }

    public void Evaluate(RuleContext context, RuleResult result)
    {
        var selectedIds = new HashSet<int>();

        // Add activation exercises that target detected compensation areas
        foreach (var compensation in context.AllCompensations)
        {
            var matchingActivation = _exerciseLibrary
                .Where(e => e.Category == ExerciseCategory.Activation
                    && !string.IsNullOrEmpty(e.CorrectsCompensations)
                    && e.CorrectsCompensations.Contains(compensation.ToString()))
                .ToList();

            foreach (var exercise in matchingActivation)
            {
                if (selectedIds.Add(exercise.Id))
                {
                    result.WarmupExercises.Add(new ExercisePrescription
                    {
                        ExerciseId = exercise.Id,
                        Sets = 2,
                        RepsMin = 10,
                        RepsMax = 12,
                        Tempo = "2-1-2-0",
                        RestSeconds = 30,
                        OrderIndex = result.WarmupExercises.Count + 1
                    });
                }
            }
        }

        // Always include general warmup exercises
        var generalWarmups = _exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Warmup)
            .Take(3)
            .ToList();

        foreach (var exercise in generalWarmups)
        {
            if (selectedIds.Add(exercise.Id))
            {
                result.WarmupExercises.Add(new ExercisePrescription
                {
                    ExerciseId = exercise.Id,
                    Sets = 1,
                    RepsMin = 10,
                    RepsMax = 15,
                    Tempo = "1-0-1-0",
                    RestSeconds = 15,
                    OrderIndex = result.WarmupExercises.Count + 1
                });
            }
        }

        // Limit warmup to 5 exercises
        if (result.WarmupExercises.Count > 5)
        {
            result.WarmupExercises = result.WarmupExercises
                .Take(5)
                .Select((e, i) => { e.OrderIndex = i + 1; return e; })
                .ToList();
        }
    }
}

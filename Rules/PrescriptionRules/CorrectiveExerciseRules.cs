using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules.PrescriptionRules;

public class CorrectiveExerciseRules : IRuleSet
{
    private readonly List<Exercise> _exerciseLibrary;

    public CorrectiveExerciseRules(List<Exercise> exerciseLibrary)
    {
        _exerciseLibrary = exerciseLibrary;
    }

    public void Evaluate(RuleContext context, RuleResult result)
    {
        var selectedIds = new HashSet<int>();

        foreach (var compensation in context.AllCompensations)
        {
            var matchingExercises = _exerciseLibrary
                .Where(e => e.Category == ExerciseCategory.Corrective
                    && !string.IsNullOrEmpty(e.CorrectsCompensations)
                    && e.CorrectsCompensations.Contains(compensation.ToString()))
                .ToList();

            foreach (var exercise in matchingExercises)
            {
                if (selectedIds.Add(exercise.Id))
                {
                    result.CorrectiveExercises.Add(new ExercisePrescription
                    {
                        ExerciseId = exercise.Id,
                        Sets = 1,
                        RepsMin = 10,
                        RepsMax = 15,
                        Tempo = "2-2-2-0",
                        RestSeconds = 30,
                        OrderIndex = result.CorrectiveExercises.Count + 1,
                        Notes = $"Corrects: {compensation}"
                    });
                }
            }
        }

        // Limit to max corrective exercises per workout
        if (result.CorrectiveExercises.Count > AppConstants.MaxCorrectiveExercisesPerWorkout)
        {
            result.CorrectiveExercises = result.CorrectiveExercises
                .OrderByDescending(e => CountCompensationsAddressed(e.ExerciseId, context.AllCompensations))
                .Take(AppConstants.MaxCorrectiveExercisesPerWorkout)
                .Select((e, i) => { e.OrderIndex = i + 1; return e; })
                .ToList();
        }
    }

    private int CountCompensationsAddressed(int exerciseId, List<MovementCompensation> compensations)
    {
        var exercise = _exerciseLibrary.FirstOrDefault(e => e.Id == exerciseId);
        if (exercise == null || string.IsNullOrEmpty(exercise.CorrectsCompensations))
            return 0;
        return compensations.Count(c => exercise.CorrectsCompensations.Contains(c.ToString()));
    }
}

using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IPrescriptionEngine
{
    Task<WorkoutProgram> GenerateProgramAsync(int userId, int assessmentSessionId);
    List<Exercise> SelectCorrectiveExercises(List<MovementCompensation> compensations, List<Exercise> exerciseLibrary);
    List<Exercise> SelectWarmupExercises(List<MovementCompensation> compensations, List<Exercise> exerciseLibrary);
    List<Exercise> SelectMainExercises(ExercisePhase phase, ActivityLevel level, List<Exercise> exerciseLibrary);
    List<Exercise> SelectCooldownExercises(List<MovementCompensation> compensations, List<Exercise> exerciseLibrary);
}

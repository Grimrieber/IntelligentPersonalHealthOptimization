using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IExercisePerformanceService
{
    /// <summary>Most recent set logs for this exercise on this workout day.</summary>
    Task<List<ExercisePerformanceEntry>> GetForDayAsync(int userId, int workoutDayId, int exerciseId);

    /// <summary>Insert or update a single set's weight + reps.</summary>
    Task SaveSetAsync(int userId, int workoutDayId, int exerciseId, int setNumber, decimal? weightKg, int repsCompleted);

    /// <summary>Most recent log of this exercise from any day, ordered newest first. Used by progress views.</summary>
    Task<List<ExercisePerformanceEntry>> GetHistoryAsync(int userId, int exerciseId, int limit = 20);
}

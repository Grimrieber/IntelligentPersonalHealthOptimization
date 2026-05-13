using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IScheduleService
{
    Task<List<ScheduledWorkout>> GenerateScheduleAsync(int userId, int workoutProgramId, int trainingProfileId);
    Task<List<ScheduledWorkout>> GetScheduledWorkoutsAsync(int userId, DateTime startDate, DateTime endDate);
    Task<ScheduledWorkout?> GetTodaysWorkoutAsync(int userId);
    Task<WorkoutCompletion> CompleteWorkoutAsync(int scheduledWorkoutId, int durationMinutes, int difficulty, string? notes);
    Task SkipWorkoutAsync(int scheduledWorkoutId);
    Task RescheduleWorkoutAsync(int scheduledWorkoutId, DateTime newDate);
    Task<(int completed, int missed, int skipped, int total)> GetCompletionStatsAsync(int userId, int days = 30);
    Task<int> GetStreakAsync(int userId);
}

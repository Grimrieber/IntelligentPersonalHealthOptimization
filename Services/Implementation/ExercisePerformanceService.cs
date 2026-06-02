using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class ExercisePerformanceService : IExercisePerformanceService
{
    private readonly IDatabaseService _databaseService;

    public ExercisePerformanceService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<ExercisePerformanceEntry>> GetForDayAsync(int userId, int workoutDayId, int exerciseId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<ExercisePerformanceEntry>()
            .Where(p => p.UserId == userId && p.WorkoutDayId == workoutDayId && p.ExerciseId == exerciseId)
            .OrderBy(p => p.SetNumber)
            .ToListAsync();
    }

    public async Task SaveSetAsync(int userId, int workoutDayId, int exerciseId, int setNumber,
                                   decimal? weightKg, int repsCompleted)
    {
        var db = await _databaseService.GetConnectionAsync();
        var existing = await db.Table<ExercisePerformanceEntry>()
            .FirstOrDefaultAsync(p => p.UserId == userId
                && p.WorkoutDayId == workoutDayId
                && p.ExerciseId == exerciseId
                && p.SetNumber == setNumber);

        if (existing == null)
        {
            await db.InsertAsync(new ExercisePerformanceEntry
            {
                UserId = userId,
                WorkoutDayId = workoutDayId,
                ExerciseId = exerciseId,
                SetNumber = setNumber,
                WeightKg = weightKg,
                RepsCompleted = repsCompleted,
                LoggedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.WeightKg = weightKg;
            existing.RepsCompleted = repsCompleted;
            existing.LoggedAt = DateTime.UtcNow;
            await db.UpdateAsync(existing);
        }
    }

    public async Task<List<ExercisePerformanceEntry>> GetHistoryAsync(int userId, int exerciseId, int limit = 20)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<ExercisePerformanceEntry>()
            .Where(p => p.UserId == userId && p.ExerciseId == exerciseId)
            .OrderByDescending(p => p.LoggedAt)
            .Take(limit)
            .ToListAsync();
    }
}

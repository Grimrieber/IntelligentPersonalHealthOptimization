using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class ScheduleService : IScheduleService
{
    private readonly IDatabaseService _databaseService;

    public ScheduleService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<ScheduledWorkout>> GenerateScheduleAsync(int userId, int workoutProgramId, int trainingProfileId)
    {
        var profile = await _databaseService.GetByIdAsync<TrainingProfile>(trainingProfileId);
        var program = await _databaseService.GetByIdAsync<WorkoutProgram>(workoutProgramId);
        if (profile == null || program == null)
            throw new InvalidOperationException("Training profile or workout program not found");

        var db = await _databaseService.GetConnectionAsync();
        var workoutDays = await db.Table<WorkoutDay>()
            .Where(d => d.WorkoutProgramId == workoutProgramId)
            .OrderBy(d => d.DayNumber)
            .ToListAsync();

        // Parse available days
        var availableDays = profile.AvailableDays
            .Split(',', StringSplitOptions.TrimEntries)
            .Select(d => Enum.Parse<DayOfWeek>(d))
            .ToList();

        var scheduledWorkouts = new List<ScheduledWorkout>();
        var startDate = DateTime.Today;
        var endDate = startDate.AddDays(program.DurationWeeks * 7);
        var currentDate = startDate;
        var dayIndex = 0;

        while (currentDate <= endDate)
        {
            if (availableDays.Contains(currentDate.DayOfWeek) && workoutDays.Count > 0)
            {
                var workoutDay = workoutDays[dayIndex % workoutDays.Count];

                // Parse preferred time
                var reminderHour = profile.PreferredTimeOfDay switch
                {
                    "Morning" => 7,
                    "Afternoon" => 13,
                    "Evening" => 18,
                    _ => 9
                };

                var workout = new ScheduledWorkout
                {
                    UserId = userId,
                    WorkoutDayId = workoutDay.Id,
                    ScheduledDate = currentDate,
                    Status = WorkoutStatus.Scheduled,
                    ReminderTime = currentDate.AddHours(reminderHour).AddMinutes(-30),
                    Notes = $"{workoutDay.DayName} - {workoutDay.Focus}"
                };

                await _databaseService.InsertAsync(workout);
                scheduledWorkouts.Add(workout);
                dayIndex++;
            }

            currentDate = currentDate.AddDays(1);
        }

        return scheduledWorkouts;
    }

    public async Task<List<ScheduledWorkout>> GetScheduledWorkoutsAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<ScheduledWorkout>()
            .Where(w => w.UserId == userId && w.ScheduledDate >= startDate.Date && w.ScheduledDate <= endDate.Date)
            .OrderBy(w => w.ScheduledDate)
            .ToListAsync();
    }

    public async Task<ScheduledWorkout?> GetTodaysWorkoutAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        return await db.Table<ScheduledWorkout>()
            .Where(w => w.UserId == userId && w.ScheduledDate >= today && w.ScheduledDate < tomorrow)
            .FirstOrDefaultAsync();
    }

    public async Task<WorkoutCompletion> CompleteWorkoutAsync(int scheduledWorkoutId, int durationMinutes, int difficulty, string? notes)
    {
        var workout = await _databaseService.GetByIdAsync<ScheduledWorkout>(scheduledWorkoutId);
        if (workout == null) throw new InvalidOperationException("Scheduled workout not found");

        workout.Status = WorkoutStatus.Completed;
        await _databaseService.UpdateAsync(workout);

        var completion = new WorkoutCompletion
        {
            ScheduledWorkoutId = scheduledWorkoutId,
            CompletedDate = DateTime.UtcNow,
            DurationMinutes = durationMinutes,
            Difficulty = difficulty,
            Notes = notes
        };

        await _databaseService.InsertAsync(completion);
        return completion;
    }

    public async Task SkipWorkoutAsync(int scheduledWorkoutId)
    {
        var workout = await _databaseService.GetByIdAsync<ScheduledWorkout>(scheduledWorkoutId);
        if (workout == null) return;

        workout.Status = WorkoutStatus.Skipped;
        await _databaseService.UpdateAsync(workout);
    }

    public async Task RescheduleWorkoutAsync(int scheduledWorkoutId, DateTime newDate)
    {
        var workout = await _databaseService.GetByIdAsync<ScheduledWorkout>(scheduledWorkoutId);
        if (workout == null) return;

        workout.ScheduledDate = newDate.Date;
        workout.Status = WorkoutStatus.Rescheduled;
        await _databaseService.UpdateAsync(workout);
    }

    public async Task<(int completed, int missed, int skipped, int total)> GetCompletionStatsAsync(int userId, int days = 30)
    {
        var db = await _databaseService.GetConnectionAsync();
        var cutoff = DateTime.Today.AddDays(-days);
        var workouts = await db.Table<ScheduledWorkout>()
            .Where(w => w.UserId == userId && w.ScheduledDate >= cutoff && w.ScheduledDate <= DateTime.Today)
            .ToListAsync();

        // Mark past scheduled workouts as missed
        foreach (var w in workouts.Where(w => w.Status == WorkoutStatus.Scheduled && w.ScheduledDate < DateTime.Today))
        {
            w.Status = WorkoutStatus.Missed;
            await _databaseService.UpdateAsync(w);
        }

        return (
            workouts.Count(w => w.Status == WorkoutStatus.Completed),
            workouts.Count(w => w.Status == WorkoutStatus.Missed),
            workouts.Count(w => w.Status == WorkoutStatus.Skipped),
            workouts.Count
        );
    }

    public async Task<int> GetStreakAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        var workouts = await db.Table<ScheduledWorkout>()
            .Where(w => w.UserId == userId && w.ScheduledDate <= DateTime.Today)
            .OrderByDescending(w => w.ScheduledDate)
            .ToListAsync();

        int streak = 0;
        foreach (var w in workouts)
        {
            if (w.Status == WorkoutStatus.Completed)
                streak++;
            else if (w.Status == WorkoutStatus.Missed)
                break;
        }

        return streak;
    }
}

using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using Plugin.LocalNotification;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class NotificationService : Interfaces.INotificationService
{
    private static int _nextNotificationId = 1000;

    private readonly IDatabaseService _databaseService;

    public NotificationService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<int> ScheduleWorkoutReminderAsync(ScheduledWorkout workout, DateTime reminderTime)
    {
        if (reminderTime <= DateTime.Now) return -1;

        var notificationId = Interlocked.Increment(ref _nextNotificationId);

        var notification = new NotificationRequest
        {
            NotificationId = notificationId,
            Title = "Workout Reminder",
            Description = workout.Notes ?? "Time for your workout!",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = reminderTime
            }
        };

        await LocalNotificationCenter.Current.Show(notification);
        return notificationId;
    }

    public async Task CancelReminderAsync(int notificationId)
    {
        LocalNotificationCenter.Current.Cancel(notificationId);
        await Task.CompletedTask;
    }

    public async Task CancelAllRemindersAsync()
    {
        LocalNotificationCenter.Current.CancelAll();
        await Task.CompletedTask;
    }

    public async Task<int> ScheduleDailyReminderAsync(TimeSpan timeOfDay, string title, string message)
    {
        var notificationId = Interlocked.Increment(ref _nextNotificationId);

        var now = DateTime.Now;
        var notifyTime = now.Date.Add(timeOfDay);
        if (notifyTime <= now)
            notifyTime = notifyTime.AddDays(1);

        var notification = new NotificationRequest
        {
            NotificationId = notificationId,
            Title = title,
            Description = message,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime,
                RepeatType = NotificationRepeat.Daily
            }
        };

        await LocalNotificationCenter.Current.Show(notification);
        return notificationId;
    }

    public async Task<bool> AreNotificationsEnabledAsync()
    {
        var result = await LocalNotificationCenter.Current.AreNotificationsEnabled();
        return result;
    }

    public async Task<bool> RequestPermissionAsync()
    {
        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    // Fixed notification-id ranges so a reschedule (CancelAll + re-create) is fully idempotent.
    private const int WorkoutIdBase = 100;  // 100..106 (one per weekday)
    private const int MealId = 200;
    private const int WaterIdBase = 300;    // 300..

    public async Task RescheduleForUserAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        var settings = await db.Table<NotificationSettings>().FirstOrDefaultAsync(n => n.UserId == userId);
        if (settings == null)
        {
            LocalNotificationCenter.Current.CancelAll();
            return;
        }

        var profile = await db.Table<TrainingProfile>().FirstOrDefaultAsync(t => t.UserId == userId);
        var days = new List<DayOfWeek>();
        foreach (var part in (profile?.AvailableDays ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<DayOfWeek>(part, true, out var d))
                days.Add(d);

        await ApplyScheduleAsync(settings, days);
    }

    public async Task ApplyScheduleAsync(NotificationSettings settings, IReadOnlyList<DayOfWeek> trainingDays)
    {
        // Clear everything first; we re-create from scratch so stale slots never linger.
        LocalNotificationCenter.Current.CancelAll();

        if (settings.WorkoutEnabled && trainingDays is { Count: > 0 })
        {
            var time = TimeSpan.FromMinutes(settings.WorkoutTimeMinutes);
            foreach (var day in trainingDays.Distinct())
            {
                await ShowWeeklyAsync(WorkoutIdBase + (int)day, day, time,
                    "Workout Reminder", "Time to train 💪");
            }
        }

        if (settings.MealEnabled)
        {
            await ShowDailyAsync(MealId, TimeSpan.FromMinutes(settings.MealTimeMinutes),
                "Meal Reminder", "Don't forget to log your meals 🍽️");
        }

        if (settings.WaterEnabled)
        {
            var step = Math.Max(1, settings.WaterIntervalHours) * 60;
            var slot = 0;
            for (var m = settings.WaterStartMinutes; m <= settings.WaterEndMinutes; m += step)
            {
                await ShowDailyAsync(WaterIdBase + slot, TimeSpan.FromMinutes(m),
                    "Hydration", "Time to drink some water 💧");
                slot++;
            }
        }
    }

    // Weekly-repeating notification anchored to the next occurrence of the given weekday/time.
    private static async Task ShowWeeklyAsync(int id, DayOfWeek day, TimeSpan time, string title, string description)
    {
        var now = DateTime.Now;
        var delta = ((int)day - (int)now.DayOfWeek + 7) % 7;
        var notifyTime = now.Date.AddDays(delta).Add(time);
        if (notifyTime <= now)
            notifyTime = notifyTime.AddDays(7);

        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = id,
            Title = title,
            Description = description,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime,
                RepeatType = NotificationRepeat.Weekly
            }
        });
    }

    // Daily-repeating notification at the given time-of-day (next occurrence).
    private static async Task ShowDailyAsync(int id, TimeSpan time, string title, string description)
    {
        var now = DateTime.Now;
        var notifyTime = now.Date.Add(time);
        if (notifyTime <= now)
            notifyTime = notifyTime.AddDays(1);

        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = id,
            Title = title,
            Description = description,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime,
                RepeatType = NotificationRepeat.Daily
            }
        });
    }
}

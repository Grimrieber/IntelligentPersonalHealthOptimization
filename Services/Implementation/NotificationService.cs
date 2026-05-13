using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using Plugin.LocalNotification;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class NotificationService : Interfaces.INotificationService
{
    private static int _nextNotificationId = 1000;

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
}

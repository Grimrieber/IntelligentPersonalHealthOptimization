using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface INotificationService
{
    Task<int> ScheduleWorkoutReminderAsync(ScheduledWorkout workout, DateTime reminderTime);
    Task CancelReminderAsync(int notificationId);
    Task CancelAllRemindersAsync();
    Task<int> ScheduleDailyReminderAsync(TimeSpan timeOfDay, string title, string message);
    Task<bool> AreNotificationsEnabledAsync();
}

using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface INotificationService
{
    Task<int> ScheduleWorkoutReminderAsync(ScheduledWorkout workout, DateTime reminderTime);
    Task CancelReminderAsync(int notificationId);
    Task CancelAllRemindersAsync();
    Task<int> ScheduleDailyReminderAsync(TimeSpan timeOfDay, string title, string message);
    Task<bool> AreNotificationsEnabledAsync();

    /// <summary>Prompt the OS for notification permission (Android 13+/iOS). Returns granted.</summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>Cancel everything and re-create all reminders from the user's settings.
    /// Workout reminders fire weekly on each training day; meal + water are daily repeats.
    /// Idempotent — safe to call on every save and on app launch.</summary>
    Task ApplyScheduleAsync(NotificationSettings settings, IReadOnlyList<DayOfWeek> trainingDays);

    /// <summary>Load the user's saved NotificationSettings + training days from the DB and
    /// (re)apply the schedule. Call on app launch so reminders survive reboots/app updates.</summary>
    Task RescheduleForUserAsync(int userId);
}

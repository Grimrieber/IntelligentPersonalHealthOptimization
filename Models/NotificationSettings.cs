using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

/// <summary>Per-user local-notification preferences. Times are stored as minutes past
/// midnight (local) so they map cleanly in SQLite. One row per user.</summary>
public class NotificationSettings
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    // Workout reminders fire at WorkoutTime on each of the user's training days (AvailableDays).
    public bool WorkoutEnabled { get; set; }
    public int WorkoutTimeMinutes { get; set; } = 18 * 60; // 6:00 PM

    // Single daily "log your meals" nudge.
    public bool MealEnabled { get; set; }
    public int MealTimeMinutes { get; set; } = 12 * 60; // 12:00 PM

    // Recurring hydration reminders every WaterIntervalHours between start and end.
    public bool WaterEnabled { get; set; }
    public int WaterStartMinutes { get; set; } = 8 * 60;  // 8:00 AM
    public int WaterEndMinutes { get; set; } = 20 * 60;   // 8:00 PM
    public int WaterIntervalHours { get; set; } = 2;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

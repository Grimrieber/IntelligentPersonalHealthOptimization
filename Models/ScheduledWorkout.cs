using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class ScheduledWorkout
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public int? WorkoutDayId { get; set; }

    [Indexed]
    public DateTime ScheduledDate { get; set; }

    public WorkoutStatus Status { get; set; } = WorkoutStatus.Scheduled;

    public DateTime? ReminderTime { get; set; }
    public int? NotificationId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

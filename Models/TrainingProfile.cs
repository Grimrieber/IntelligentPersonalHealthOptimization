using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class TrainingProfile
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public TrainingLocation TrainingLocation { get; set; }

    public ExperienceLevel ExperienceLevel { get; set; }

    public int TrainingMonths { get; set; }

    public int CurrentFrequency { get; set; }

    public int SessionDurationMinutes { get; set; }

    [MaxLength(500)]
    public string AvailableEquipment { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AvailableDays { get; set; } = string.Empty;

    [MaxLength(50)]
    public string PreferredTimeOfDay { get; set; } = string.Empty;

    [MaxLength(200)]
    public string SportOrActivity { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("TrainingEnvironment")]
public class TrainingEnvironment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public CeilingHeight HomeCeiling { get; set; }
    public NoiseTolerance HomeNoise { get; set; }
    public FloorSpace HomeFloorSpace { get; set; }
    public bool HasOutdoorAccess { get; set; }

    [MaxLength(100)]
    public string GymChain { get; set; } = string.Empty;

    public TrainingTimeBand UsualGymTime { get; set; }

    [MaxLength(500)]
    public string GymRestrictions { get; set; } = string.Empty;

    public bool IncludeCardio { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

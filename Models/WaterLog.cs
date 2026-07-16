using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

/// <summary>
/// One row per user per day recording glasses of water drunk, so the water
/// tracker persists across app restarts instead of resetting to zero.
/// </summary>
public class WaterLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    /// <summary>The day this count is for (date only).</summary>
    [Indexed]
    public DateTime LogDate { get; set; }

    public int Glasses { get; set; }
}

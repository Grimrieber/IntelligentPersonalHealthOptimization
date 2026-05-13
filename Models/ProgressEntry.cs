using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("ProgressEntries")]
public class ProgressEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public DateTime EntryDate { get; set; }

    public double? WeightKg { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

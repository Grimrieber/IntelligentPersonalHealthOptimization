using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("EquipmentInventory")]
public class EquipmentInventoryItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public EquipmentItemType ItemType { get; set; }

    public TrainingLocation Location { get; set; }

    public decimal? MaxLoadKg { get; set; }
    public decimal? MinLoadKg { get; set; }
    public decimal? IncrementKg { get; set; }

    [MaxLength(200)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("GymChainEquipmentTemplates")]
public class GymChainEquipmentTemplate
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(100), NotNull]
    public string ChainName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string TypicalEquipment { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Restrictions { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Notes { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

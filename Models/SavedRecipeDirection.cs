using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class SavedRecipeDirection
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SavedRecipeId { get; set; }

    public int StepNumber { get; set; }

    [MaxLength(100)]
    public string? DirectionGroup { get; set; }

    public string Instruction { get; set; } = string.Empty;
}

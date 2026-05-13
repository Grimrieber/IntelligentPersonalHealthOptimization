using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class SavedRecipeIngredient
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SavedRecipeId { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(100)]
    public string? IngredientGroup { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}

using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class Food
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Brand { get; set; }

    public FoodCategory FoodCategory { get; set; }

    public double CaloriesPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public double CarbsPer100g { get; set; }
    public double FatPer100g { get; set; }
    public double FiberPer100g { get; set; }

    public double DefaultServingSize { get; set; } = 100;

    [MaxLength(50)]
    public string DefaultServingLabel { get; set; } = "100g";

    [Indexed, MaxLength(50)]
    public string? Barcode { get; set; }

    public bool IsUserCreated { get; set; }
    public bool IsActive { get; set; } = true;
}

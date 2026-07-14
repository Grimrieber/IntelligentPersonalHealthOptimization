using Microsoft.Maui.Graphics;
using IntelligentPersonalHealthOptimization.Data;

namespace IntelligentPersonalHealthOptimization.Models.Recipe;

/// <summary>
/// Read-only model mapped from MSSQL RecipeDB.Categories table.
/// </summary>
public class RecipeCategory
{
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public byte[]? CategoryImage { get; set; }

    // Populated by queries
    public int RecipeCount { get; set; }

    // Presentation: emoji + accent colour for the cookbook tiles (see RecipeCategoryStyle).
    public string Emoji => RecipeCategoryStyle.EmojiFor(CategoryName);
    public Color Accent => RecipeCategoryStyle.AccentFor(CategoryName);
}

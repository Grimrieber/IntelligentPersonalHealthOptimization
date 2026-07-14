using Microsoft.Maui.Graphics;

namespace IntelligentPersonalHealthOptimization.Data;

/// <summary>
/// Single source of truth for the health-tier badge chip (label + colour) shown on
/// recipe cards, recipe detail, and meal-plan cards — so every surface stays in
/// sync. Colours match the review-dashboard palette. See <see cref="RecipeHealth"/>.
/// </summary>
public static class HealthBadgeStyle
{
    public static bool ShouldShow(string? tier) => !string.IsNullOrEmpty(tier);

    /// <summary>Chip label — "Treat" for a lean sweet, otherwise the tier name.</summary>
    public static string TextFor(string? tier, bool isTreat)
        => isTreat ? "Treat" : (tier ?? string.Empty);

    public static Color ColorFor(string? tier) => tier switch
    {
        RecipeHealth.Healthy => Color.FromArgb("#1F8A4C"),
        RecipeHealth.Moderate => Color.FromArgb("#B9791A"),
        RecipeHealth.Indulgent => Color.FromArgb("#BB5340"),
        _ => Colors.Gray,
    };
}

using System.Text.Json;
using Microsoft.Maui.Storage;

namespace IntelligentPersonalHealthOptimization.Data;

/// <summary>One recently-viewed recipe. Id is the catalog SavedRecipe row id
/// (the value the detail page opens with).</summary>
public record RecentRecipe(int Id, string Name, string? ImageUrl, string? Tier, int? Calories)
{
    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
    public string CaloriesText => Calories is int c && c > 0 ? $"{c} cal" : string.Empty;
}

/// <summary>
/// A lightweight, self-contained "recently viewed recipes" list persisted in
/// <see cref="Preferences"/> as JSON. Stores just enough to render the
/// "Jump back in" strip on the Cookbook landing without a DB round-trip; the
/// tapped id reopens the recipe via RecipeDetail?recipeId=. Most-recent-first,
/// deduped by id, capped at <see cref="MaxItems"/>.
/// </summary>
public static class RecentRecipes
{
    private const string Key = "recent_recipes_v1";
    public const int MaxItems = 8;

    public static List<RecentRecipe> Get()
    {
        try
        {
            var json = Preferences.Get(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return new();
            return JsonSerializer.Deserialize<List<RecentRecipe>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>Record a view: moves the recipe to the front, dedupes by id,
    /// caps the list. No-op on invalid ids.</summary>
    public static void Add(int id, string name, string? imageUrl, string? tier, int? calories)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var list = Get();
            list.RemoveAll(r => r.Id == id);
            list.Insert(0, new RecentRecipe(id, name, imageUrl, tier, calories));
            if (list.Count > MaxItems) list = list.GetRange(0, MaxItems);
            Preferences.Set(Key, JsonSerializer.Serialize(list));
        }
        catch
        {
            // Non-critical — a failed write just means the strip is stale.
        }
    }
}

using System.Text.Json;
using Microsoft.Maui.Storage;

namespace IntelligentPersonalHealthOptimization.Data;

/// <summary>One ad-hoc shopping line — an ingredient description tagged with the
/// recipe it came from.</summary>
public record ShopLine(string Recipe, string Description);

/// <summary>
/// Ad-hoc shopping-list items added from a recipe's "Add to Shopping List"
/// button, persisted in <see cref="Preferences"/> as JSON. These merge into the
/// meal-plan-derived grocery list (and make the list usable with no meal plan).
/// </summary>
public static class ExtraShoppingItems
{
    private const string Key = "extra_shopping_items_v1";

    public static List<ShopLine> Get()
    {
        try
        {
            var json = Preferences.Get(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return new();
            return JsonSerializer.Deserialize<List<ShopLine>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>Append a recipe's ingredient lines. Skips lines already present
    /// from the same recipe so re-adding the same recipe doesn't duplicate.</summary>
    public static void Add(string recipeName, IEnumerable<string> descriptions)
    {
        try
        {
            var recipe = string.IsNullOrWhiteSpace(recipeName) ? "Recipe" : recipeName.Trim();
            var list = Get();
            var existing = new HashSet<string>(
                list.Where(l => l.Recipe == recipe).Select(l => l.Description),
                StringComparer.OrdinalIgnoreCase);

            foreach (var d in descriptions)
            {
                if (string.IsNullOrWhiteSpace(d)) continue;
                var desc = d.Trim();
                if (existing.Add(desc))
                    list.Add(new ShopLine(recipe, desc));
            }
            Preferences.Set(Key, JsonSerializer.Serialize(list));
        }
        catch
        {
            // Non-critical.
        }
    }

    public static void Clear() => Preferences.Remove(Key);

    public static int Count() => Get().Count;
}

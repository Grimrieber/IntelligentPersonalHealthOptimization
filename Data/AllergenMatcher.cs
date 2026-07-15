using System.Text.RegularExpressions;

namespace IntelligentPersonalHealthOptimization.Data;

/// <summary>
/// Flags recipe ingredient lines that hit the user's allergies or foods-to-avoid.
/// The profile stores allergies as comma-joined FoodAllergy names (e.g. "Dairy,Peanuts")
/// and foods-to-avoid as free text; this expands allergy categories to ingredient
/// keywords and word-boundary-matches each line. Advisory + keyword-based (may miss
/// hidden/derived ingredients) — not a substitute for reading the full list.
/// </summary>
public static class AllergenMatcher
{
    private static readonly Dictionary<string, string[]> Expand =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gluten"] = new[] { "wheat", "flour", "bread", "pasta", "barley", "rye", "couscous",
                             "cracker", "noodle", "semolina", "bulgur", "seitan", "malt", "farro" },
        ["Wheat"] = new[] { "wheat", "flour", "bread", "pasta", "semolina", "couscous" },
        ["Dairy"] = new[] { "milk", "cheese", "butter", "cream", "yogurt", "yoghurt", "ghee",
                            "whey", "custard", "parmesan", "mozzarella", "cheddar", "ricotta",
                            "buttermilk", "feta", "paneer" },
        ["TreeNuts"] = new[] { "almond", "walnut", "pecan", "cashew", "pistachio", "hazelnut",
                               "macadamia", "brazil nut", "pine nut" },
        ["Peanuts"] = new[] { "peanut" },
        ["Shellfish"] = new[] { "shrimp", "prawn", "crab", "lobster", "clam", "mussel",
                                "oyster", "scallop", "squid", "crayfish" },
        ["Fish"] = new[] { "fish", "salmon", "tuna", "cod", "tilapia", "anchovy", "sardine",
                           "mackerel", "trout", "halibut" },
        ["Soy"] = new[] { "soy", "soya", "tofu", "tempeh", "edamame", "miso" },
        ["Eggs"] = new[] { "egg", "mayonnaise", "meringue", "albumen" },
        ["Sesame"] = new[] { "sesame", "tahini" },
        ["Sulfites"] = new[] { "sulfite", "sulphite", "wine" },
    };

    public sealed class Term
    {
        public string Label = string.Empty;
        public Regex Pattern = null!;
    }

    /// <summary>Build the match terms from the profile's allergies + foods-to-avoid.</summary>
    public static List<Term> Build(string? allergies, string? foodsToAvoid)
    {
        var terms = new List<Term>();
        foreach (var a in Split(allergies))
        {
            if (a.Equals("None", StringComparison.OrdinalIgnoreCase)) continue;
            var kws = Expand.TryGetValue(a, out var e) ? e : new[] { a };
            terms.Add(new Term { Label = Friendly(a), Pattern = BuildPattern(kws) });
        }
        foreach (var f in Split(foodsToAvoid))
            terms.Add(new Term { Label = f, Pattern = BuildPattern(new[] { f }) });
        return terms;
    }

    /// <summary>Label of the first term that matches the line, or null.</summary>
    public static string? Match(string? line, List<Term> terms)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        foreach (var t in terms)
            if (t.Pattern.IsMatch(line))
                return t.Label;
        return null;
    }

    private static string[] Split(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? Array.Empty<string>()
            : s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static Regex BuildPattern(string[] keywords) =>
        new(@"\b(?:" + string.Join("|", keywords.Select(Regex.Escape)) + ")",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string Friendly(string allergy) => allergy switch
    {
        "TreeNuts" => "tree nuts",
        _ => allergy.ToLowerInvariant(),
    };
}

using System.Globalization;
using System.Text.RegularExpressions;

namespace IntelligentPersonalHealthOptimization.Data;

/// <summary>
/// Turns free-text recipe ingredient lines into a consolidated, aisle-grouped
/// shopping list. Ingredients are stored as raw strings ("2 cloves garlic,
/// smashed") with no structured quantity/unit/aisle, so this normalises each line
/// to a key (strips the leading amount + units + prep), buckets it into a grocery
/// aisle by keyword, and merges duplicates. Quantity *summing* is deliberately not
/// attempted — the source text is too irregular; the raw amounts are kept as detail.
/// Pure/deterministic. Mirrors the analysis style of RecipeHealth.
/// </summary>
public static class GroceryList
{
    // Display order of aisles (matched by name).
    public const string Produce = "Produce";
    public const string MeatSeafood = "Meat & Seafood";
    public const string DairyEggs = "Dairy & Eggs";
    public const string Bakery = "Bakery";
    public const string PantryDry = "Pantry & Dry Goods";
    public const string Spices = "Spices & Seasonings";
    public const string Frozen = "Frozen";
    public const string Other = "Other";

    public static readonly string[] AisleOrder =
    {
        Produce, MeatSeafood, DairyEggs, Bakery, PantryDry, Spices, Frozen, Other,
    };

    public static string AisleEmoji(string aisle) => aisle switch
    {
        Produce => "\U0001F955",       // carrot
        MeatSeafood => "\U0001F969",   // cut of meat
        DairyEggs => "\U0001F9C0",     // cheese
        Bakery => "\U0001F35E",        // bread
        PantryDry => "\U0001F35A",     // rice bowl
        Spices => "\U0001F9C2",        // salt
        Frozen => "❄️",      // snowflake
        _ => "\U0001F6D2",             // trolley
    };

    /// <summary>One aggregated line item on the shopping list.</summary>
    public sealed class Entry
    {
        public string Aisle = Other;
        public string Name = string.Empty;              // display name, e.g. "Garlic"
        public List<string> Amounts = new();            // raw lines, e.g. "2 cloves garlic, smashed"
        public List<string> Recipes = new();            // recipes that need it
    }

    /// <summary>Build the aggregated list from (recipeName, ingredientLine) pairs.</summary>
    public static List<Entry> Build(IEnumerable<(string recipe, string description)> lines)
    {
        var byKey = new Dictionary<string, Entry>();
        foreach (var (recipe, description) in lines)
        {
            if (string.IsNullOrWhiteSpace(description))
                continue;
            var (aisle, name) = Classify(description);
            var mapKey = aisle + "|" + name.ToLowerInvariant();
            if (!byKey.TryGetValue(mapKey, out var e))
            {
                e = new Entry { Aisle = aisle, Name = name };
                byKey[mapKey] = e;
            }
            e.Amounts.Add(description.Trim());
            if (!string.IsNullOrWhiteSpace(recipe) && !e.Recipes.Contains(recipe))
                e.Recipes.Add(recipe);
        }
        return byKey.Values
            .OrderBy(e => Array.IndexOf(AisleOrder, e.Aisle))
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>(aisle, displayName) for one ingredient line.</summary>
    public static (string aisle, string name) Classify(string description)
    {
        var full = Parens.Replace(description.ToLowerInvariant(), " ");
        return (Aisle(full), NormalizeName(full));
    }

    // ---- name normalization ----------------------------------------------
    private static readonly Regex Parens = new(@"\([^)]*\)", RegexOptions.Compiled);
    // Leading amount: digits, unicode fractions, ranges, decimals, separators.
    // Fractions listed explicitly (no char-class range) so the static regex can't
    // fail to compile at class-load.
    private static readonly Regex LeadingQty =
        new(@"^[0-9\s\./\-–x×½¼¾⅓⅔⅛⅜⅝⅞⅕⅖⅗⅘⅙⅚]+", RegexOptions.Compiled);

    private static readonly string[] Units =
    {
        "tablespoons","tablespoon","tbsps","tbsp","teaspoons","teaspoon","tsps","tsp",
        "cups","cup","ounces","ounce","oz","pounds","pound","lbs","lb","grams","gram",
        "kilograms","kilogram","kgs","kg","grams","gram","g","milliliters","milliliter","ml","liters","liter",
        "pints","pint","quarts","quart","gallons","gallon","cloves","clove","cans","can",
        "packages","package","packets","packet","pkgs","pkg","jars","jar","bottles","bottle",
        "pinches","pinch","dashes","dash","slices","slice","sticks","stick","stalks","stalk",
        "sprigs","sprig","heads","head","bunches","bunch","handfuls","handful","cubes","cube",
        "strips","strip","fillets","fillet","pieces","piece","cloves","tin","tins",
        "each","ea","large","medium","small","whole",
    };

    private static string NormalizeName(string full)
    {
        // Key off the part before the first comma (drops trailing prep like ", chopped").
        var comma = full.IndexOf(',');
        var head = (comma > 0 ? full[..comma] : full).Trim();
        head = LeadingQty.Replace(head, "").Trim();
        // Drop periods so abbreviated units keep matching ("ea." -> "ea", "oz." -> "oz").
        head = head.Replace(".", "");

        bool removed = true;
        while (removed && head.Length > 0)
        {
            removed = false;
            foreach (var u in Units)
            {
                if (head == u) { head = ""; removed = true; break; }
                if (head.StartsWith(u + " ", StringComparison.Ordinal))
                {
                    head = head[(u.Length + 1)..].Trim();
                    head = LeadingQty.Replace(head, "").Trim(); // e.g. "1 1/2 cups" leftovers
                    removed = true;
                    break;
                }
            }
        }
        if (head.StartsWith("of ", StringComparison.Ordinal))
            head = head[3..].Trim();

        if (string.IsNullOrWhiteSpace(head))
            head = (comma > 0 ? full[..comma] : full).Trim();
        if (string.IsNullOrWhiteSpace(head))
            head = full.Trim();

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(head);
    }

    // ---- aisle classification (first match wins; order matters) -----------
    private static bool Has(string t, params string[] keys)
    {
        foreach (var k in keys)
            if (t.Contains(k, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string Aisle(string t)
    {
        if (Has(t, "frozen", "ice cream")) return Frozen;

        // Dairy & eggs (before pantry so "butter"/"cream" land here) — but keep the
        // nut/seed/plant butters and non-dairy milks in pantry.
        if (Has(t, "peanut butter", "almond butter", "cashew butter", "nut butter",
                   "cocoa butter", "coconut milk", "almond milk", "soy milk", "oat milk"))
            return PantryDry;
        if (Has(t, "egg", "milk", "cheese", "butter", "cream", "yogurt", "yoghurt", "ghee",
                   "parmesan", "mozzarella", "cheddar", "feta", "ricotta", "buttermilk",
                   "mascarpone", "custard", "paneer"))
            return DairyEggs;

        if (Has(t, "tortilla", "bread crumb", "breadcrumb")) return PantryDry; // crumbs are pantry
        if (Has(t, "bread", "bun", "roll", "baguette", "naan", "pita", "bagel", "croissant",
                   "brioche"))
            return Bakery;

        // Pantry liquids / condiments / staples (before meat so "chicken stock",
        // "fish sauce" land in pantry).
        if (Has(t, "oil", "vinegar", "sauce", "stock", "broth", "bouillon", "paste",
                   "honey", "syrup", "molasses", "sugar", "flour", "rice", "pasta", "noodle",
                   "oats", "oatmeal", "cereal", "lentil", "chickpea", "canned", "tinned",
                   "cornstarch", "corn starch", "baking powder", "baking soda", "yeast",
                   "cocoa", "chocolate", "raisin", "coconut", "gelatin", "jam", "jelly",
                   "peanut", "almond", "cashew", "walnut", "pecan", "sesame", "soy sauce",
                   "ketchup", "mustard", "mayonnaise", "mayo", "beans", "quinoa", "couscous"))
            return PantryDry;

        // Spices & seasonings.
        if (Has(t, "salt", "pepper", "cumin", "coriander", "paprika", "cinnamon", "nutmeg",
                   "cardamom", "turmeric", "oregano", "basil leaf", "bay leaf", "chili powder",
                   "chilli powder", "cayenne", "curry powder", "spice", "seasoning", "vanilla",
                   "saffron", "allspice", "clove", "thyme", "rosemary", "sage", "dill",
                   "fennel", "anise", "peppercorn"))
            return Spices;

        // Meat & seafood.
        if (Has(t, "chicken", "beef", "pork", "lamb", "mutton", "veal", "turkey", "duck",
                   "bacon", "sausage", "ham", "steak", "mince", "ground beef", "chorizo",
                   "pepperoni", "prosciutto", "salami", "meat", "fish", "salmon", "tuna",
                   "shrimp", "prawn", "crab", "lobster", "cod", "tilapia", "anchovy",
                   "sardine", "seafood", "mussel", "clam", "oyster", "scallop", "squid"))
            return MeatSeafood;

        // Produce (fruits, veg, fresh herbs).
        if (Has(t, "onion", "garlic", "tomato", "potato", "carrot", "bell pepper",
                   "green pepper", "red pepper", "chili", "chilli", "jalapeno", "celery",
                   "cucumber", "lettuce", "spinach", "kale", "broccoli", "cauliflower",
                   "zucchini", "mushroom", "ginger", "lemon", "lime", "orange", "apple",
                   "banana", "berry", "avocado", "cilantro", "parsley", "mint", "scallion",
                   "green onion", "leek", "shallot", "cabbage", "corn", "pea", "squash",
                   "pumpkin", "eggplant", "cucumber", "radish", "beet", "turnip", "asparagus",
                   "grape", "mango", "pineapple", "peach", "pear", "plum", "cherry", "melon",
                   "herb", "sprout", "chard", "okra", "yam", "plantain"))
            return Produce;

        return Other;
    }
}

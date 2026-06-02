namespace IntelligentPersonalHealthOptimization.Data;

/// <summary>
/// Maps each raw Wikibooks category name (e.g. "Chicken recipes", "Nigerian
/// recipes") to one of a small set of top-level groups, so the cookbook can show
/// ~14 browsable groups that drill down into the real categories. Pure code — no
/// data migration — so the grouping is easy to tweak without touching the DB.
/// </summary>
public static class RecipeCategoryGroups
{
    public const string Other = "Other";

    /// <summary>Display order of the top-level groups (most-used first; Other last).</summary>
    public static readonly string[] Order =
    {
        "Main Dishes", "World Cuisines", "Desserts & Sweets", "Soups & Stews",
        "Sauces & Condiments", "Sides & Vegetables", "Breads & Baking", "Drinks",
        "Salads", "Snacks & Appetizers", "Breakfast", "Fruit",
        "Vegetarian & Vegan", Other,
    };

    public static int DisplayOrder(string group)
    {
        var i = System.Array.IndexOf(Order, group);
        return i < 0 ? Order.Length : i;
    }

    // Cuisine / regional adjectives → "World Cuisines" (matched as whole words).
    private static readonly HashSet<string> Cuisines = new(StringComparer.OrdinalIgnoreCase)
    {
        "african","albanian","american","arab","argentine","argentinian","asian","australian",
        "austrian","armenian","bangladeshi","belgian","bengali","brazilian","british","bulgarian",
        "cajun","cambodian","cameroonian","canadian","caribbean","chechen","chilean","chile",
        "chinese","colombian","creole","croatian","cuban","czech","danish","dutch","egyptian",
        "english","ethiopian","filipino","finnish","french","gambian","german","ghanaian","greek",
        "haitian","hawaiian","hungarian","indian","indonesian","iranian","iraqi","irish","israeli",
        "italian","jamaican","japanese","jewish","kenyan","korean","laotian","lebanese","liberian",
        "libyan","louisiana","malaysian","maltese","manx","mediterranean","mexican","moroccan",
        "nepali","nigerian","norwegian","pakistani","persian","peruvian","polish","portuguese",
        "romanian","russian","rwandan","scandinavian","scottish","senegalese","serbian","sicilian",
        "sindhi","singaporean","somali","southwestern","spanish","sylheti","syrian","taiwanese",
        "tanzanian","tex-mex","thai","tibetan","tunisian","turkish","ugandan","ukrainian",
        "venezuelan","vietnamese","welsh","zambian","zimbabwean",
    };

    public static string GroupFor(string? categoryName)
    {
        var c = (categoryName ?? string.Empty).ToLowerInvariant()
            .Replace(" recipes", string.Empty)
            .Replace(" recipe", string.Empty)
            .Trim();
        if (c.Length == 0) return Other;

        foreach (var w in c.Split(new[] { ' ', '/', '&', '-' }, StringSplitOptions.RemoveEmptyEntries))
            if (Cuisines.Contains(w)) return "World Cuisines";

        bool Has(params string[] keys)
        {
            foreach (var k in keys) if (c.Contains(k)) return true;
            return false;
        }

        if (Has("soup", "stew", "chowder", "broth", "bisque", "gumbo", "chili")) return "Soups & Stews";
        if (Has("salad")) return "Salads";
        if (Has("dessert", "cake", "cookie", "pie", "pudding", "ice cream", "candy", "chocolate",
                "marshmallow", "custard", "pastry", "tart", "brownie", "fudge", "sweet", "doughnut",
                "donut", "frosting", "cheesecake", "toffee", "truffle", "confection", "caramel",
                "cobbler", "meringue", "mousse", "souffl")) return "Desserts & Sweets";
        if (Has("bread", "muffin", "biscuit", "roll", "bun", "bagel", "scone", "dough", "loaf",
                "cornbread", "focaccia", "baguette", "batter", "baking", "baked", "flour", "wheat",
                "semolina")) return "Breads & Baking";
        if (Has("pancake", "waffle", "breakfast", "cereal", "oatmeal", "porridge", "granola",
                "french toast", "oat")) return "Breakfast";
        if (Has("beverage", "drink", "tea", "coffee", "wine", "cocktail", "smoothie", "juice",
                "soda", "milk", "latte", "lemonade", "punch", "liqueur", "beer", "margarita")) return "Drinks";
        if (Has("sauce", "dressing", "jam", "jelly", "marinade", "salsa", "dip", "gravy", "stuffing",
                "spice", "seasoning", "chutney", "relish", "condiment", "pesto", "syrup", "butter",
                "preserve", "pickle", "mayonnaise", "stock", "vinegar", "filling")) return "Sauces & Condiments";
        if (Has("snack", "appetizer", "hors", "finger food", "canape", "fritter")) return "Snacks & Appetizers";
        if (Has("chicken", "beef", "pork", "lamb", "meat", "fish", "seafood", "rice", "pasta",
                "noodle", "curry", "casserole", "egg", "tofu", "liver", "bison", "goat", "ham",
                "sausage", "meatball", "biryani", "stir", "turkey", "duck", "shrimp", "salmon",
                "steak", "burger", "pizza", "taco", "burrito", "risotto", "paella", "kebab", "roast",
                "poultry", "venison", "veal", "crab", "lobster", "bacon", "sandwich", "dumpling",
                "swallow", "sushi", "main course", "main dish", "dinner", "macaroni", "mutton",
                "clam", "oyster", "tuna", "iguana")) return "Main Dishes";
        if (Has("vegan", "vegetarian")) return "Vegetarian & Vegan";
        if (Has("fruit", "apple", "apricot", "banana", "berry", "cherry", "peach", "pear", "plum",
                "mango", "pineapple", "orange", "lemon", "lime", "fig", "date", "grape", "melon",
                "avocado", "coconut", "rhubarb")) return "Fruit";
        if (Has("vegetable", "potato", "bean", "grain", "corn", "side", "leek", "carrot",
                "cauliflower", "broccoli", "spinach", "mushroom", "tomato", "onion", "pea", "squash",
                "pumpkin", "lentil", "quinoa", "couscous", "cabbage", "eggplant", "zucchini",
                "asparagus", "okra", "plantain", "yam", "taro", "garlic", "olive", "millet", "bulgur",
                "soy", "sesame", "nut")) return "Sides & Vegetables";

        return Other;
    }
}

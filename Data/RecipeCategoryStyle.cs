using Microsoft.Maui.Graphics;

namespace IntelligentPersonalHealthOptimization.Data;

/// <summary>
/// Visual treatment (emoji + accent colour) for cookbook tiles.
///
/// Two layers:
///  1. <see cref="ByGroup"/> — the 14 top-level group names get a fixed emoji/colour
///     (used for the group grid, and as the last-resort fallback).
///  2. <see cref="Keywords"/> — an ORDERED keyword table so each real sub-category
///     ("Chicken recipes", "Salmon recipes", "Curry"…) resolves its OWN icon/colour
///     instead of inheriting the group's. First keyword contained in the (normalised)
///     name wins, so overlap-prone entries (eggplant→egg, steak→tea, goat→oat,
///     cheesecake→cheese, pineapple→apple, sweet-potato→potato) are listed first.
///
/// Pure presentation — no data, no DB.
/// </summary>
public static class RecipeCategoryStyle
{
    private readonly record struct Style(string Emoji, string Hex);

    // Family accent colours.
    private const string Meat      = "#E5533D";
    private const string Sea       = "#2196F3";
    private const string Dish      = "#FB8C00";
    private const string Grain     = "#C08457";
    private const string Dessert   = "#E91E63";
    private const string Bread     = "#B07D4F";
    private const string Breakfast = "#F9A825";
    private const string Drink     = "#29B6F6";
    private const string Sauce     = "#8D6E63";
    private const string Fruit     = "#EF5350";
    private const string Veg       = "#43A047";
    private const string Snack     = "#FF7043";
    private const string Cuisine   = "#009688";

    private static readonly Dictionary<string, Style> ByGroup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Main Dishes"]          = new("🍽️", Meat),
        ["World Cuisines"]       = new("🌍", Cuisine),
        ["Desserts & Sweets"]    = new("🍰", Dessert),
        ["Soups & Stews"]        = new("🍲", Dish),
        ["Sauces & Condiments"]  = new("🧂", Sauce),
        ["Sides & Vegetables"]   = new("🥦", Veg),
        ["Breads & Baking"]      = new("🍞", Bread),
        ["Drinks"]               = new("🥤", Drink),
        ["Salads"]               = new("🥗", "#7CB342"),
        ["Snacks & Appetizers"]  = new("🧀", Snack),
        ["Breakfast"]            = new("🍳", Breakfast),
        ["Fruit"]                = new("🍓", Fruit),
        ["Vegetarian & Vegan"]   = new("🌱", "#66BB6A"),
        [RecipeCategoryGroups.Other] = new("🍴", "#78909C"),
    };

    // Ordered: first key contained in the normalised name wins. Overlap-fixers first.
    private static readonly (string Key, string Emoji, string Hex)[] Keywords =
    {
        // ---- overlap fixers (must precede their shorter substrings) ----
        ("sweet potato", "🍠", Veg),     ("cheesecake", "🍰", Dessert),
        ("french toast", "🍞", Breakfast), ("buttermilk", "🥛", Drink),
        ("butternut", "🎃", Veg),        ("popcorn", "🍿", Snack),
        ("cornbread", "🍞", Bread),      ("licorice", "🍬", Dessert),
        ("pineapple", "🍍", Fruit),      ("peanut", "🥜", Snack),
        ("coconut", "🥥", Fruit),        ("eggplant", "🍆", Veg),
        ("steak", "🥩", Meat),           ("goat", "🐐", Meat),
        ("hamburger", "🍔", Meat),       ("meatball", "🍖", Meat),
        ("meatloaf", "🍖", Meat),        ("chickpea", "🫘", Veg),
        ("doughnut", "🍩", Dessert),     ("donut", "🍩", Dessert),
        ("blueberry", "🫐", Fruit),      ("strawberry", "🍓", Fruit),
        ("raspberry", "🍓", Fruit),      ("blackberry", "🍓", Fruit),
        ("cranberry", "🍓", Fruit),      ("peach", "🍑", Fruit),
        ("pear", "🍐", Fruit),           ("grapefruit", "🍊", Fruit),

        // ---- seafood ----
        ("shrimp", "🦐", Sea),   ("prawn", "🦐", Sea),   ("crab", "🦀", Sea),
        ("lobster", "🦞", Sea),  ("clam", "🦪", Sea),    ("oyster", "🦪", Sea),
        ("mussel", "🦪", Sea),   ("scallop", "🦪", Sea), ("squid", "🦑", Sea),
        ("octopus", "🐙", Sea),  ("salmon", "🐟", Sea),  ("tuna", "🐟", Sea),
        ("cod", "🐟", Sea),      ("halibut", "🐟", Sea), ("trout", "🐟", Sea),
        ("sardine", "🐟", Sea),  ("sushi", "🍣", Sea),   ("sashimi", "🍣", Sea),
        ("seafood", "🦐", Sea),  ("fish", "🐟", Sea),

        // ---- meat & poultry ----
        ("bacon", "🥓", Meat),   ("sausage", "🌭", Meat), ("hot dog", "🌭", Meat),
        ("burger", "🍔", Meat),  ("beef", "🥩", Meat),    ("veal", "🥩", Meat),
        ("pork", "🍖", Meat),    ("ham", "🍖", Meat),     ("lamb", "🍖", Meat),
        ("mutton", "🍖", Meat),  ("bison", "🦬", Meat),   ("buffalo", "🦬", Meat),
        ("venison", "🦌", Meat), ("rabbit", "🐰", Meat),  ("duck", "🦆", Meat),
        ("turkey", "🦃", Meat),  ("quail", "🐦", Meat),   ("poultry", "🍗", Meat),
        ("chicken", "🍗", Meat), ("liver", "🍖", Meat),   ("meat", "🍖", Meat),

        // ---- dishes ----
        ("pizza", "🍕", Dish),    ("taco", "🌮", Dish),      ("burrito", "🌯", Dish),
        ("quesadilla", "🌯", Dish), ("enchilada", "🌯", Dish), ("sandwich", "🥪", Dish),
        ("panini", "🥪", Dish),   ("wrap", "🌯", Dish),      ("macaroni", "🍝", Dish),
        ("spaghetti", "🍝", Dish), ("lasagna", "🍝", Dish),  ("pasta", "🍝", Dish),
        ("ramen", "🍜", Dish),    ("pho", "🍜", Dish),       ("noodle", "🍜", Dish),
        ("biryani", "🍚", Grain), ("risotto", "🍚", Grain),  ("paella", "🥘", Dish),
        ("pilaf", "🍚", Grain),   ("curry", "🍛", Dish),     ("casserole", "🥘", Dish),
        ("stir", "🥘", Dish),     ("kebab", "🍢", Dish),     ("skewer", "🍢", Dish),
        ("dumpling", "🥟", Dish), ("gyoza", "🥟", Dish),     ("potsticker", "🥟", Dish),
        ("spring roll", "🥟", Dish), ("egg roll", "🥟", Dish), ("quiche", "🥧", Dish),
        ("frittata", "🍳", Breakfast), ("omelet", "🍳", Breakfast), ("tofu", "🍲", Veg),
        ("tempeh", "🍲", Veg),    ("seitan", "🍲", Veg),

        // ---- soups (distinct within the Soups & Stews sub-grid) ----
        ("chowder", "🥣", Dish), ("bisque", "🥣", Dish), ("gumbo", "🫕", Dish),
        ("broth", "🫕", Dish),   ("stock", "🫕", Sauce), ("stew", "🥘", Dish),
        ("chili", "🌶️", Dish),   ("soup", "🍲", Dish),

        // ---- salad ----
        ("salad", "🥗", "#7CB342"), ("slaw", "🥗", "#7CB342"),

        // ---- desserts ----
        ("ice cream", "🍨", Dessert), ("gelato", "🍨", Dessert), ("sorbet", "🍨", Dessert),
        ("cupcake", "🧁", Dessert),  ("muffin", "🧁", Dessert),  ("cake", "🍰", Dessert),
        ("cookie", "🍪", Dessert),   ("brownie", "🍫", Dessert), ("biscuit", "🍪", Dessert),
        ("pie", "🥧", Dessert),      ("tart", "🥧", Dessert),    ("cobbler", "🥧", Dessert),
        ("crumble", "🥧", Dessert),  ("pudding", "🍮", Dessert), ("custard", "🍮", Dessert),
        ("flan", "🍮", Dessert),     ("mousse", "🍮", Dessert),  ("souffl", "🍮", Dessert),
        ("meringue", "🥧", Dessert), ("chocolate", "🍫", Dessert), ("fudge", "🍫", Dessert),
        ("truffle", "🍫", Dessert),  ("candy", "🍬", Dessert),   ("caramel", "🍬", Dessert),
        ("toffee", "🍬", Dessert),   ("marshmallow", "🍬", Dessert), ("pastry", "🥐", Dessert),
        ("croissant", "🥐", Dessert), ("danish", "🥐", Dessert), ("eclair", "🥐", Dessert),
        ("macaron", "🍬", Dessert),  ("dessert", "🍰", Dessert), ("sweet", "🍬", Dessert),

        // ---- breakfast / bread ----
        ("pancake", "🥞", Breakfast), ("waffle", "🧇", Breakfast), ("toast", "🍞", Breakfast),
        ("cereal", "🥣", Breakfast), ("oatmeal", "🥣", Breakfast), ("porridge", "🥣", Breakfast),
        ("granola", "🥣", Breakfast), ("oat", "🥣", Breakfast),   ("egg", "🥚", Breakfast),
        ("breakfast", "🍳", Breakfast), ("bagel", "🥯", Bread),   ("baguette", "🥖", Bread),
        ("roll", "🥖", Bread),       ("bun", "🍞", Bread),        ("loaf", "🍞", Bread),
        ("dough", "🍞", Bread),      ("focaccia", "🍞", Bread),   ("pretzel", "🥨", Bread),
        ("scone", "🍞", Bread),      ("bread", "🍞", Bread),      ("flour", "🌾", Bread),
        ("wheat", "🌾", Bread),

        // ---- drinks ----
        ("coffee", "☕", Drink),  ("latte", "☕", Drink),    ("espresso", "☕", Drink),
        ("cappuccino", "☕", Drink), ("tea", "🍵", Drink),   ("wine", "🍷", Drink),
        ("sangria", "🍷", Drink), ("beer", "🍺", Drink),     ("cider", "🍺", Drink),
        ("cocktail", "🍸", Drink), ("martini", "🍸", Drink), ("margarita", "🍹", Drink),
        ("mojito", "🍹", Drink),  ("smoothie", "🥤", Drink), ("milkshake", "🥤", Drink),
        ("shake", "🥤", Drink),   ("juice", "🧃", Drink),    ("soda", "🥤", Drink),
        ("lemonade", "🍋", Drink), ("punch", "🍹", Drink),   ("liqueur", "🍸", Drink),
        ("beverage", "🥤", Drink), ("drink", "🥤", Drink),   ("milk", "🥛", Drink),

        // ---- sauces / condiments ----
        ("sauce", "🥫", Sauce),   ("salsa", "🍅", Sauce),    ("gravy", "🥣", Sauce),
        ("dressing", "🫙", Sauce), ("marinade", "🫙", Sauce), ("pesto", "🌿", Sauce),
        ("syrup", "🍯", Sauce),   ("honey", "🍯", Sauce),    ("jam", "🍓", Sauce),
        ("jelly", "🍇", Sauce),   ("marmalade", "🍊", Sauce), ("preserve", "🫙", Sauce),
        ("pickle", "🥒", Sauce),  ("chutney", "🫙", Sauce),  ("relish", "🫙", Sauce),
        ("ketchup", "🍅", Sauce), ("mayonnaise", "🥚", Sauce), ("vinaigrette", "🫙", Sauce),
        ("vinegar", "🫙", Sauce), ("stock", "🍲", Sauce),    ("spice", "🧂", Sauce),
        ("seasoning", "🧂", Sauce), ("condiment", "🫙", Sauce), ("dip", "🥣", Sauce),
        ("hummus", "🥣", Sauce),  ("butter", "🧈", Sauce),

        // ---- fruit ----
        ("apple", "🍎", Fruit),  ("banana", "🍌", Fruit),   ("berry", "🍓", Fruit),
        ("cherry", "🍒", Fruit), ("apricot", "🍑", Fruit),  ("nectarine", "🍑", Fruit),
        ("plum", "🍑", Fruit),   ("mango", "🥭", Fruit),    ("orange", "🍊", Fruit),
        ("tangerine", "🍊", Fruit), ("clementine", "🍊", Fruit), ("lemon", "🍋", Fruit),
        ("lime", "🍋", Fruit),   ("grape", "🍇", Fruit),    ("raisin", "🍇", Fruit),
        ("watermelon", "🍉", Fruit), ("melon", "🍈", Fruit), ("cantaloupe", "🍈", Fruit),
        ("fig", "🍈", Fruit),    ("date", "🌴", Fruit),     ("kiwi", "🥝", Fruit),
        ("avocado", "🥑", Fruit), ("pomegranate", "🍎", Fruit), ("papaya", "🥭", Fruit),
        ("guava", "🥭", Fruit),  ("fruit", "🍓", Fruit),

        // ---- vegetables / grains ----
        ("potato", "🥔", Veg),   ("fries", "🍟", Veg),      ("lentil", "🫘", Veg),
        ("bean", "🫘", Veg),     ("corn", "🌽", Veg),       ("carrot", "🥕", Veg),
        ("cauliflower", "🥦", Veg), ("broccoli", "🥦", Veg), ("spinach", "🥬", Veg),
        ("kale", "🥬", Veg),     ("lettuce", "🥬", Veg),    ("cabbage", "🥬", Veg),
        ("mushroom", "🍄", Veg), ("tomato", "🍅", Veg),     ("onion", "🧅", Veg),
        ("garlic", "🧄", Veg),   ("pea", "🫛", Veg),        ("squash", "🎃", Veg),
        ("pumpkin", "🎃", Veg),  ("zucchini", "🥒", Veg),   ("cucumber", "🥒", Veg),
        ("pepper", "🫑", Veg),   ("okra", "🥬", Veg),       ("plantain", "🍌", Veg),
        ("yam", "🍠", Veg),      ("taro", "🥔", Veg),       ("olive", "🫒", Veg),
        ("quinoa", "🌾", Grain), ("couscous", "🌾", Grain), ("millet", "🌾", Grain),
        ("bulgur", "🌾", Grain), ("barley", "🌾", Grain),   ("rice", "🍚", Grain),
        ("grain", "🌾", Grain),  ("asparagus", "🥬", Veg),  ("beet", "🥬", Veg),
        ("radish", "🥬", Veg),   ("celery", "🥬", Veg),     ("artichoke", "🥬", Veg),
        ("sprout", "🥬", Veg),   ("turnip", "🥬", Veg),     ("parsnip", "🥕", Veg),
        ("leek", "🥬", Veg),     ("chard", "🥬", Veg),      ("collard", "🥬", Veg),
        ("vegetable", "🥦", Veg), ("veggie", "🥦", Veg),

        // ---- nuts / snacks ----
        ("almond", "🌰", Snack), ("walnut", "🌰", Snack),   ("pecan", "🌰", Snack),
        ("cashew", "🌰", Snack), ("pistachio", "🌰", Snack), ("hazelnut", "🌰", Snack),
        ("nut", "🥜", Snack),    ("cracker", "🍘", Snack),  ("chip", "🍟", Snack),
        ("crisp", "🍟", Snack),  ("cheese", "🧀", Snack),   ("nacho", "🧀", Snack),
        ("fritter", "🧆", Snack), ("falafel", "🧆", Snack), ("samosa", "🥟", Snack),
        ("snack", "🍿", Snack),  ("appetizer", "🧀", Snack), ("canape", "🧀", Snack),

        // ---- cuisines (World Cuisines sub-categories) — country flags for uniqueness ----
        ("italian", "🇮🇹", Cuisine),  ("mexican", "🇲🇽", Cuisine),  ("tex-mex", "🌮", Cuisine),
        ("chinese", "🇨🇳", Cuisine),  ("japanese", "🇯🇵", Cuisine), ("indian", "🇮🇳", Cuisine),
        ("thai", "🇹🇭", Cuisine),     ("vietnamese", "🇻🇳", Cuisine), ("korean", "🇰🇷", Cuisine),
        ("french", "🇫🇷", Cuisine),   ("greek", "🇬🇷", Cuisine),    ("spanish", "🇪🇸", Cuisine),
        ("german", "🇩🇪", Cuisine),   ("british", "🇬🇧", Cuisine),  ("english", "🫖", Cuisine),
        ("scottish", "🏴󠁧󠁢󠁳󠁣󠁴󠁿", Cuisine), ("irish", "🇮🇪", Cuisine),    ("american", "🇺🇸", Cuisine),
        ("cajun", "🦐", Cuisine),    ("creole", "🎺", Cuisine),   ("caribbean", "🌴", Cuisine),
        ("jamaican", "🇯🇲", Cuisine), ("cuban", "🇨🇺", Cuisine),    ("brazilian", "🇧🇷", Cuisine),
        ("peruvian", "🇵🇪", Cuisine), ("argentin", "🇦🇷", Cuisine), ("moroccan", "🇲🇦", Cuisine),
        ("ethiopian", "🇪🇹", Cuisine), ("lebanese", "🇱🇧", Cuisine), ("turkish", "🇹🇷", Cuisine),
        ("persian", "🇮🇷", Cuisine),  ("iranian", "🇮🇷", Cuisine),  ("egyptian", "🇪🇬", Cuisine),
        ("nigerian", "🇳🇬", Cuisine), ("ghanaian", "🇬🇭", Cuisine), ("kenyan", "🇰🇪", Cuisine),
        ("zambian", "🇿🇲", Cuisine),  ("cameroonian", "🇨🇲", Cuisine), ("rwandan", "🇷🇼", Cuisine),
        ("african", "🌍", Cuisine),  ("mediterranean", "🫒", Cuisine), ("russian", "🇷🇺", Cuisine),
        ("polish", "🇵🇱", Cuisine),   ("hungarian", "🇭🇺", Cuisine), ("portuguese", "🇵🇹", Cuisine),
        ("filipino", "🇵🇭", Cuisine), ("indonesian", "🇮🇩", Cuisine), ("malaysian", "🇲🇾", Cuisine),
        ("hawaiian", "🍍", Cuisine),
    };

    private static Style Resolve(string? categoryName)
    {
        var raw = categoryName ?? string.Empty;

        // Exact top-level group (group grid + last-resort fallback).
        if (ByGroup.TryGetValue(raw, out var g)) return g;

        var norm = raw.ToLowerInvariant()
            .Replace(" recipes", string.Empty)
            .Replace(" recipe", string.Empty)
            .Trim();

        foreach (var (key, emoji, hex) in Keywords)
            if (norm.Contains(key)) return new Style(emoji, hex);

        // Nothing matched — inherit the parent group's style.
        return ByGroup.TryGetValue(RecipeCategoryGroups.GroupFor(raw), out var grp)
            ? grp
            : ByGroup[RecipeCategoryGroups.Other];
    }

    public static string EmojiFor(string? categoryName) => Resolve(categoryName).Emoji;

    public static Color AccentFor(string? categoryName) => Color.FromArgb(Resolve(categoryName).Hex);
}

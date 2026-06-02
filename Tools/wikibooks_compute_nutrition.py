"""
Wikibooks Nutrition Computation
================================
For each row in WIKIBOOKS_Recipes, parse its ingredient lines, match each
against the Foods library (SeedData.Foods.cs), convert quantity+unit to grams,
sum per-100g macros, divide by Servings, and insert into WIKIBOOKS_RecipeNutrition.

Then audits which ingredient food-name strings did NOT match anything — that's
the list of foods worth adding to the library next.

Usage:
    python wikibooks_compute_nutrition.py            # compute (preserves existing rows)
    python wikibooks_compute_nutrition.py --reload   # wipe RecipeNutrition then recompute
"""

import json
import os
import re
import sys
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path

import pyodbc

HERE = Path(__file__).parent
REPO = HERE.parent
FOODS_CS = REPO / "Data" / "SeedData.Foods.cs"
USDA_INDEX = HERE / "usda" / "sr_legacy_index.json"


# ---------- Foods library extraction ----------

FOOD_RE = re.compile(
    r'new\(\)\s*\{\s*Name\s*=\s*"([^"]+)"\s*,\s*'
    r'FoodCategory\s*=\s*FoodCategory\.(\w+)\s*,\s*'
    r'CaloriesPer100g\s*=\s*([\d.]+)\s*,\s*'
    r'ProteinPer100g\s*=\s*([\d.]+)\s*,\s*'
    r'CarbsPer100g\s*=\s*([\d.]+)\s*,\s*'
    r'FatPer100g\s*=\s*([\d.]+)\s*,\s*'
    r'FiberPer100g\s*=\s*([\d.]+)\s*,\s*'
    r'DefaultServingSize\s*=\s*([\d.]+)\s*,\s*'
    r'DefaultServingLabel\s*=\s*"([^"]+)"'
)


def normalize_food_key(name: str) -> str:
    """Lowercase, strip parenthetical notes, collapse whitespace."""
    s = name.lower()
    s = re.sub(r"\([^)]*\)", "", s)        # remove "(cooked)"
    s = re.sub(r"\s+", " ", s).strip()
    return s


def load_foods():
    text = FOODS_CS.read_text(encoding="utf-8")
    foods = []
    for m in FOOD_RE.finditer(text):
        name, cat, cal, pro, carb, fat, fib, srv_g, srv_lbl = m.groups()
        foods.append({
            "name": name,
            "category": cat,
            "cal": float(cal),
            "protein": float(pro),
            "carb": float(carb),
            "fat": float(fat),
            "fiber": float(fib),
            # The 4 macros our SeedData doesn't track — filled in from USDA later.
            "sodium": 0.0,
            "cholesterol": 0.0,
            "satfat": 0.0,
            "sugar": 0.0,
            "default_serving_g": float(srv_g),
            "default_serving_label": srv_lbl,
            "key": normalize_food_key(name),
        })
    return foods


# ---------- USDA SR Legacy fallback ----------

USDA_STOP_TOKENS = {
    "cooked", "raw", "fresh", "dried", "frozen", "prepared", "boiled",
    "steamed", "roasted", "baked", "fried", "grilled", "broiled", "stewed",
    "with", "without", "and", "or", "of", "in", "as", "the", "for",
    "free", "low", "high", "regular", "reduced", "fat", "lean", "extra",
    "all", "any", "various", "type", "kind", "style", "flavored",
    "ns", "nfs", "general", "use", "made", "from", "added",
    "salt", "salted", "unsalted", "sweetened", "unsweetened",
    "skin", "skinless", "bone", "boneless",
    "commercial", "homemade", "canned", "raw",
    "broiler", "broilers", "fryer", "fryers",
    "meat", "only",
}


def _usda_tokens(text):
    s = unicodedata.normalize("NFD", text).lower()
    s = "".join(c for c in s if unicodedata.category(c) != "Mn")
    s = re.sub(r"[^a-z\s]", " ", s)
    return {w for w in s.split() if len(w) >= 3 and w not in USDA_STOP_TOKENS}


def load_usda():
    """Load Tools/usda/sr_legacy_index.json plus build a token inverted index."""
    if not USDA_INDEX.exists():
        print(f"  USDA index not found at {USDA_INDEX} — fallback disabled")
        return None, None
    data = json.loads(USDA_INDEX.read_text(encoding="utf-8"))
    inv = defaultdict(set)
    for i, f in enumerate(data):
        for t in _usda_tokens(f["description"]):
            inv[t].add(i)
    print(f"  USDA index: {len(data):,} foods, {len(inv):,} unique tokens")
    return data, inv


def _adapt_usda_food(usda):
    """Convert a USDA index entry to the dict shape match_food returns."""
    return {
        "name": usda["description"],
        "key": usda["description"].lower(),
        "category": "USDA",
        "cal":         usda.get("cal", 0.0),
        "protein":     usda.get("protein", 0.0),
        "carb":        usda.get("carb", 0.0),
        "fat":         usda.get("fat", 0.0),
        "fiber":       usda.get("fiber", 0.0),
        "sodium":      usda.get("sodium", 0.0),
        "cholesterol": usda.get("cholesterol", 0.0),
        "satfat":      usda.get("satfat", 0.0),
        "sugar":       usda.get("sugar", 0.0),
        "default_serving_g": 100.0,
        "default_serving_label": None,
    }


_usda_match_cache = {}


def usda_match(phrase, usda_data, usda_inv, min_score=1):
    """Find the best USDA match for an ingredient phrase via token overlap.
    Returns an adapted food dict, or None if no reasonable match."""
    if not usda_data:
        return None
    norm = normalize_ingredient_phrase(phrase) if not isinstance(phrase, set) else None
    cache_key = norm if norm is not None else phrase
    if cache_key in _usda_match_cache:
        return _usda_match_cache[cache_key]

    q_tokens = _usda_tokens(norm) if norm else set()
    if not q_tokens:
        _usda_match_cache[cache_key] = None
        return None

    scores = defaultdict(int)
    for t in q_tokens:
        for idx in usda_inv.get(t, ()):
            scores[idx] += 1
    if not scores:
        _usda_match_cache[cache_key] = None
        return None

    # Pick the food with the highest token overlap. Tiebreak: prefer shorter
    # descriptions (fewer "extra" tokens means a tighter match).
    best_idx = max(
        scores,
        key=lambda i: (scores[i], -len(_usda_tokens(usda_data[i]["description"]))),
    )
    if scores[best_idx] < min_score:
        _usda_match_cache[cache_key] = None
        return None

    adapted = _adapt_usda_food(usda_data[best_idx])
    _usda_match_cache[cache_key] = adapted
    return adapted


def enrich_foods_from_usda(foods, usda_data, usda_inv):
    """For each SeedData food, find a USDA match and copy the 4 missing macros."""
    if not usda_data:
        return 0
    enriched = 0
    for f in foods:
        match = usda_match(f["name"], usda_data, usda_inv)
        if match:
            # Only copy fields the SeedData entry doesn't supply
            f["sodium"]      = match["sodium"]
            f["cholesterol"] = match["cholesterol"]
            f["satfat"]      = match["satfat"]
            f["sugar"]       = match["sugar"]
            enriched += 1
    # Clear the cache so subsequent recipe lookups can use it for ingredient
    # phrases, not just Foods enrichment.
    _usda_match_cache.clear()
    return enriched


# ---------- Ingredient string parsing ----------

UNICODE_FRACTIONS = {
    "¼": 0.25, "½": 0.5, "¾": 0.75,
    "⅐": 1/7, "⅑": 1/9, "⅒": 0.1,
    "⅓": 1/3, "⅔": 2/3,
    "⅕": 0.2, "⅖": 0.4, "⅗": 0.6, "⅘": 0.8,
    "⅙": 1/6, "⅚": 5/6,
    "⅛": 0.125, "⅜": 0.375, "⅝": 0.625, "⅞": 0.875,
}

# Weight units: food-independent (g/unit)
WEIGHT_UNITS_G = {
    "g": 1, "gram": 1, "grams": 1,
    "kg": 1000, "kilogram": 1000, "kilograms": 1000,
    "mg": 0.001, "milligram": 0.001, "milligrams": 0.001,
    "oz": 28.35, "ounce": 28.35, "ounces": 28.35,
    "lb": 453.6, "lbs": 453.6, "pound": 453.6, "pounds": 453.6,
}

# Volume units (ml/unit). Convert to grams using per-food density.
VOLUME_UNITS_ML = {
    "cup": 240, "cups": 240, "c": 240,
    "tablespoon": 15, "tablespoons": 15, "tbsp": 15, "tbs": 15, "tb": 15, "t": 15,
    "teaspoon": 5, "teaspoons": 5, "tsp": 5,
    "ml": 1, "milliliter": 1, "milliliters": 1, "millilitre": 1, "millilitres": 1,
    "l": 1000, "liter": 1000, "liters": 1000, "litre": 1000, "litres": 1000,
    "floz": 30, "fl": 30,
}

# Small "fixed" units that aren't very food-dependent.
FIXED_SMALL_UNITS_G = {
    "pinch": 0.3, "pinches": 0.3,
    "dash": 0.6, "dashes": 0.6,
    "sprig": 1, "sprigs": 1,
}

# Per-category density fallback (g/ml). Used when food has no usable volume
# info in its DefaultServingLabel.
DEFAULT_DENSITY_G_PER_ML = 0.6     # generic dry ingredient
WATER_DENSITY_G_PER_ML  = 1.0      # liquids default

# Union of all unit keys we recognize (used during ingredient parsing).
ALL_UNITS = (
    set(WEIGHT_UNITS_G) | set(VOLUME_UNITS_ML) | set(FIXED_SMALL_UNITS_G)
    | {"clove", "cloves", "stick", "sticks", "slice", "slices"}
)

# Stripped descriptor words that don't help with food matching.
DESCRIPTORS = {
    "fresh", "freshly", "frozen", "raw", "cooked", "uncooked",
    "diced", "chopped", "minced", "sliced", "grated", "shredded",
    "ground", "crushed", "peeled", "seeded", "pitted", "halved",
    "whole", "small", "medium", "large", "extra", "ripe", "soft",
    "hot", "cold", "warm", "boiled", "steamed", "roasted", "baked",
    "to", "taste", "and", "or", "of", "for", "the", "a", "an",
    "optional", "garnish", "drained", "rinsed", "softened", "melted",
    "sifted", "packed", "loosely", "tightly",
}


def to_number(token: str):
    """Convert a numeric token (which may include unicode fractions, mixed
    numbers, or plain fractions) to float. Returns None if unparseable."""
    t = token.strip()
    if not t:
        return None
    # Replace unicode fractions with their value, possibly mixed (e.g., "1½")
    val = 0.0
    found = False
    # Handle pure unicode fraction: "½"
    if len(t) == 1 and t in UNICODE_FRACTIONS:
        return UNICODE_FRACTIONS[t]
    # Mixed unicode: "1½"
    m = re.fullmatch(r"(\d+)([¼-¾⅐-⅞])", t)
    if m:
        return float(m.group(1)) + UNICODE_FRACTIONS[m.group(2)]
    # Plain ASCII fraction "1/2"
    m = re.fullmatch(r"(\d+)\s*/\s*(\d+)", t)
    if m:
        a, b = float(m.group(1)), float(m.group(2))
        return a / b if b else None
    # Mixed: "1 1/2"
    m = re.fullmatch(r"(\d+)\s+(\d+)\s*/\s*(\d+)", t)
    if m:
        whole = float(m.group(1))
        a, b = float(m.group(2)), float(m.group(3))
        return whole + (a / b if b else 0)
    # Decimal or integer
    try:
        return float(t)
    except ValueError:
        return None


QTY_TOKEN_RE = re.compile(
    r"^\s*"
    r"("                                                    # group 1: qty expression
    r"\d+\s+\d+\s*/\s*\d+"                                  # 1 1/2
    r"|\d+\s*/\s*\d+"                                       # 1/2
    r"|\d+(?:\.\d+)?\s*[¼-¾⅐-⅞]?"       # 1, 1.5, 1½
    r"|[¼-¾⅐-⅞]"                        # ½
    r")"
    r"(?:\s*[\-–]\s*"                                  # optional "- N" or "– N"
    r"(\d+(?:\.\d+)?|\d+\s*/\s*\d+|[¼-¾⅐-⅞])"   # group 2: upper
    r")?"
)


def parse_ingredient(line: str):
    """Return (quantity_lower, quantity_upper, unit, food_phrase) — any may be None."""
    s = unicodedata.normalize("NFC", line).strip()
    # Strip leading bullets / dashes
    s = re.sub(r"^[•\-\*·]\s*", "", s)
    # Remove parenthetical asides (kept compact)
    s_for_qty = re.sub(r"\([^)]*\)", "", s)

    m = QTY_TOKEN_RE.match(s_for_qty)
    if not m:
        # No quantity — entire string is the food phrase
        return None, None, None, s

    qty = to_number(m.group(1))
    qty_upper = to_number(m.group(2)) if m.group(2) else None
    remainder = s_for_qty[m.end():].strip()

    # Strip a leading comma/punct
    remainder = re.sub(r"^[,;:\.]\s*", "", remainder)

    # Try to find a unit at the start of the remainder
    unit = None
    tokens = re.split(r"\s+", remainder, maxsplit=1)
    if tokens:
        leading = re.sub(r"[^A-Za-z]", "", tokens[0]).lower()
        if leading in ALL_UNITS:
            unit = leading
            remainder = tokens[1] if len(tokens) > 1 else ""
        else:
            two = " ".join(remainder.split()[:2]).lower().rstrip("s.,;")
            if two in {"fluid ounce", "fluid ounces"}:
                unit = "floz"
                remainder = " ".join(remainder.split()[2:])

    food_phrase = remainder.strip()
    return qty, qty_upper, unit, food_phrase


def normalize_ingredient_phrase(phrase: str) -> str:
    # NFD decomposes accented chars (jalapeño → jalapeño), then we drop
    # combining marks so 'ñ' becomes plain 'n'.
    s = unicodedata.normalize("NFD", phrase).lower()
    s = "".join(c for c in s if unicodedata.category(c) != "Mn")
    s = re.sub(r"\([^)]*\)", "", s)
    s = re.sub(r"[,;].*$", "", s)              # drop after first comma/semicolon
    s = re.sub(r"[^a-z\s\-]", " ", s)          # keep alpha + hyphen
    drop = DESCRIPTORS | EXTRA_DESCRIPTORS
    words = [w for w in s.split() if w and w not in drop]
    return " ".join(words).strip()


# Synonyms — bare/common terms in recipes that should map to a specific food key.
# Right-hand-side values must match a food's `key` (normalized name) in the library.
SYNONYMS = {
    # Eggs & dairy
    "egg": "whole egg",
    "eggs": "whole egg",
    "egg yolk": "whole egg",
    "egg yolks": "whole egg",
    "egg white": "egg whites",
    "milk": "whole milk",
    "buttermilk": "buttermilk",
    "cream": "heavy cream",
    "whipping cream": "heavy cream",
    "heavy whipping cream": "heavy cream",
    "double cream": "heavy cream",
    "cheese": "cheddar cheese",

    # Meat & seafood
    "chicken": "chicken breast",
    "chicken meat": "chicken breast",
    "chicken pieces": "chicken breast",
    "beef": "ground beef 90% lean",
    "ground beef": "ground beef 90% lean",
    "minced beef": "ground beef 90% lean",
    "meat": "ground beef 90% lean",
    "pork": "pork chop",
    "fish": "cod",
    "fish fillet": "cod",

    # Carbs
    "rice": "white rice",
    "pasta": "pasta",
    "spaghetti": "pasta",
    "penne": "pasta",
    "macaroni": "pasta",
    "linguine": "pasta",
    "fettuccine": "pasta",
    "noodles": "pasta",
    "bread": "white bread",
    "potato": "potato",
    "potatoes": "potato",
    "flour": "all-purpose flour",
    "plain flour": "all-purpose flour",
    "wheat flour": "all-purpose flour",
    "self-raising flour": "all-purpose flour",
    "self raising flour": "all-purpose flour",
    "bread flour": "all-purpose flour",
    "cake flour": "all-purpose flour",

    # Veg
    "tomato": "tomato",
    "tomatoes": "tomato",
    "onion": "onion",
    "onions": "onion",
    "scallion": "scallions",
    "scallions": "scallions",
    "green onion": "scallions",
    "green onions": "scallions",
    "spring onion": "scallions",
    "spring onions": "scallions",
    "shallot": "shallots",
    "shallots": "shallots",
    "carrot": "carrots",
    "carrots": "carrots",
    "lettuce": "lettuce",
    "iceberg lettuce": "lettuce",
    "pepper": "black pepper",
    "ground black pepper": "black pepper",
    "freshly ground black pepper": "black pepper",
    "freshly-ground black pepper": "black pepper",
    "black peppercorns": "black pepper",
    "peppercorns": "black pepper",

    # Sweeteners / extracts
    "vanilla": "vanilla extract",
    "white sugar": "sugar",
    "white granulated sugar": "sugar",
    "granulated sugar": "sugar",
    "caster sugar": "sugar",
    "icing sugar": "powdered sugar",
    "confectioners sugar": "powdered sugar",
    "confectioner's sugar": "powdered sugar",

    # Fats
    "oil": "vegetable oil",
    "cooking oil": "vegetable oil",
    "olive oil": "olive oil",
    "ghee": "ghee",

    # Spices (grounds → base)
    "garlic clove": "garlic",
    "garlic cloves": "garlic",
    "yeast": "yeast (dry)",
    "active dry yeast": "yeast (dry)",
    "instant yeast": "yeast (dry)",
    "bay leaf": "bay leaves",
    "ground cumin": "cumin",
    "ground coriander": "coriander",
    "ground ginger": "ginger",
    "ground cinnamon": "cinnamon",
    "ground nutmeg": "nutmeg",
    "ground cloves": "cloves",
    "ground turmeric": "turmeric",
    "ground cardamom": "cardamom",
    "fresh ginger": "ginger",
    "red chile powder": "chili powder",
    "chile powder": "chili powder",
    "red chili powder": "chili powder",
    "chiles": "green chiles",
    "chilies": "green chiles",
    "chillies": "green chiles",
    "green chillies": "green chiles",
    "green chilies": "green chiles",

    # Nuts (often plural in recipes)
    "nuts": "almonds",
    "pecan": "pecans",
    "pecans": "pecans",
    "walnut": "walnuts",
    "walnuts": "walnuts",
    "cashew": "cashews",
    "cashews": "cashews",
    "almond": "almonds",
    "almonds": "almonds",

    # Misc
    "ice": "ice",
    "ice cubes": "ice",
    "water": "water",
    "seasoning cube": "stock cube",
    "seasoning cubes": "stock cube",
    "maggi cube": "stock cube",
    "bouillon cube": "stock cube",
    "chocolate chips": "chocolate chips",
    "breadcrumbs": "breadcrumbs",
    "bread crumbs": "breadcrumbs",
    "cornmeal": "cornmeal",
    "olives": "black olives",
    "black olives": "black olives",
    "green olives": "green olives",
    "capers": "capers",
    "sesame seeds": "sesame seeds",
    "sesame seed": "sesame seeds",
    "plantain": "plantain",
    "plantains": "plantain",
    "yam": "yam",
    "yams": "yam",
    "bean sprouts": "bean sprouts",
    "stockfish": "stockfish",
    "dryfish": "dryfish",
    "ponmo": "ponmo",
    "berbere": "berbere",
    "berbere spice blend": "berbere",
    "garam masala": "garam masala",
    "asafoetida": "asafoetida",
    "curry leaves": "curry leaves",
    "allspice": "allspice",
    "cream of tartar": "cream of tartar",
    "cream tartar": "cream of tartar",
    "dill": "dill",
    "fresh dill": "dill",
    "chives": "chives",
    "fresh chives": "chives",
    "vegetables": "mixed vegetables",
    "mixed vegetables": "mixed vegetables",

    # Round 2 additions
    "leek": "scallions",
    "leeks": "scallions",
    "mace": "nutmeg",
    "marjoram": "oregano",
    "tarragon": "thyme",
    "italian seasoning": "oregano",
    "italian herbs": "oregano",
    "mixed spice": "allspice",
    "groundnut": "peanuts",
    "groundnuts": "peanuts",
    "groundnut oil": "vegetable oil",
    "peanut oil": "vegetable oil",
    "yogurt": "greek yogurt (plain, nonfat)",
    "yoghurt": "greek yogurt (plain, nonfat)",
    "plain yogurt": "greek yogurt (plain, nonfat)",
    "natural yogurt": "greek yogurt (plain, nonfat)",
    "prawn": "shrimp (cooked)",
    "prawns": "shrimp (cooked)",
    "bicarbonate soda": "baking soda",
    "bicarbonate of soda": "baking soda",
    "sodium bicarbonate": "baking soda",
    "potash": "baking soda",
    "chuck": "ground beef 90% lean",
    "chuck roast": "ground beef 90% lean",
    "chuck steak": "ground beef 90% lean",
    "stew meat": "ground beef 90% lean",
    "beans": "black beans (cooked)",
    "dried beans": "black beans (cooked)",
    "canned beans": "black beans (cooked)",
    "kidney beans": "kidney beans (cooked)",
    "black beans": "black beans (cooked)",
    "white beans": "kidney beans (cooked)",
    "navy beans": "kidney beans (cooked)",
    "pinto beans": "kidney beans (cooked)",
    "cocoa": "cocoa powder",
    "cacao powder": "cocoa powder",
    "stock": "chicken broth",
    "fish stock": "chicken broth",
    "broth": "chicken broth",
    "vegetable stock": "vegetable broth",
    "beef stock": "beef broth",
    "chicken stock": "chicken broth",
    "tabasco sauce": "hot sauce",
    "tabasco": "hot sauce",
    "sriracha": "hot sauce",
    "barbecue sauce": "barbecue sauce",
    "bbq sauce": "barbecue sauce",
    "oyster sauce": "oyster sauce",
    "hoisin sauce": "hoisin sauce",
    "adobo sauce": "hot sauce",
    "fish sauce": "soy sauce",
    "maggi": "stock cube",
    "maggi cube": "stock cube",
    "maggi seasoning": "stock cube",
    "jalapenos": "jalapeno pepper",
    "jalapeno": "jalapeno pepper",
    "currant": "raisins",
    "currants": "raisins",
    "sultana": "raisins",
    "sultanas": "raisins",
    "fenugreek seeds": "fenugreek seeds",
    "fenugreek": "fenugreek seeds",
    "semolina": "semolina",
    "jam": "jam",
    "preserves": "jam",
    "marmalade": "jam",
    "golden syrup": "golden syrup",
    "corn syrup": "golden syrup",
    "besan": "almond flour",
    "gram flour": "almond flour",
    "chickpea flour": "almond flour",
    "peach": "peaches",
    "peaches": "peaches",
    "brandy": "brandy",
    "rum": "rum",
    "dark rum": "rum",
    "white rum": "rum",
    "sherry": "sherry",
    "mirin": "sherry",
    "cooking wine": "sherry",
    "red wine": "sherry",
    "white wine": "sherry",
    "wine": "sherry",
    "beer": "rum",
    "vodka": "rum",
    "fermented locust beans": "stock cube",
    "iru": "stock cube",
    "uziza leaves": "curry leaves",
    "utazi leaves": "curry leaves",
    "scent leaf": "basil",
    "scent leaves": "basil",
    "bouquet garni": "thyme",
    "herbs": "oregano",
    "fresh herbs": "oregano",
    "dried herbs": "oregano",
    "spices": "allspice",
    "spice": "allspice",
    "cassava": "yam",
    "cassava flour": "all-purpose flour",
    "cooking spray": "vegetable oil",
    "chilli powder": "chili powder",
    "chipotle powder": "chili powder",
    "smoked paprika": "paprika",

    # Round 3: cheese types, herbs, nuts, regional Nigerian/Asian
    "mozzarella": "mozzarella cheese",
    "cheddar": "cheddar cheese",
    "parmesan": "parmesan cheese",
    "feta": "feta cheese",
    "sage": "thyme",
    "savory": "thyme",
    "hazelnut": "almonds",
    "hazelnuts": "almonds",
    "pistachio": "almonds",
    "pistachios": "almonds",
    "macadamia": "almonds",
    "pine nuts": "almonds",
    "sausage": "ground beef 90% lean",
    "sausages": "ground beef 90% lean",
    "ground sausage": "ground beef 90% lean",
    "italian sausage": "ground beef 90% lean",
    "liver": "ground beef 90% lean",
    "veal": "ground beef 90% lean",
    "mutton": "ground beef 90% lean",
    "lamb": "ground beef 90% lean",
    "ground lamb": "ground beef 90% lean",
    "oats": "oats (dry)",
    "oat flakes": "oats (dry)",
    "rolled oats": "oats (dry)",
    "oatmeal": "oats (dry)",
    "cocoyam": "yam",
    "kpomo": "ponmo",
    "shaki": "ponmo",
    "ukpaka": "stock cube",
    "ogiri": "stock cube",
    "iru": "stock cube",
    "locust beans": "stock cube",
    "ogbono": "stock cube",
    "shombo": "green chiles",
    "tatashe": "green chiles",
    "habanero": "jalapeno pepper",
    "scotch bonnet": "jalapeno pepper",
    "rodo": "jalapeno pepper",
    "periwinkles": "shrimp",
    "oyster": "shrimp",
    "oysters": "shrimp",
    "clams": "shrimp",
    "mussels": "shrimp",
    "lobster": "shrimp",
    "crab": "shrimp",
    "crab meat": "shrimp",
    "cognac": "brandy",
    "whiskey": "brandy",
    "whisky": "brandy",
    "bourbon": "brandy",
    "gin": "brandy",
    "tequila": "rum",
    "sake": "sherry",
    "coffee liqueur": "rum",
    "liqueur": "rum",
    "saffron": "turmeric",
    "rhubarb": "celery",
    "tamarind": "vinegar",
    "horseradish": "mustard",
    "pickles": "vinegar",
    "barbecue rub": "allspice",
    "chop rub": "allspice",
    "poultry shake": "allspice",
    "seasoning": "stock cube",
    "puff pastry": "white bread",
    "pandan leaves": "curry leaves",
    "pandan": "curry leaves",
    "poppy seeds": "sesame seeds",
    "caraway seeds": "sesame seeds",
    "fennel seeds": "sesame seeds",
    "mustard seeds": "sesame seeds",
    "anise seeds": "sesame seeds",
    "chia seeds": "sesame seeds",
    "flax seeds": "sesame seeds",
    "sunflower seeds": "sesame seeds",
    "pumpkin seeds": "sesame seeds",
    "semisweet chocolate": "chocolate chips",
    "dark chocolate": "chocolate chips",
    "milk chocolate": "chocolate chips",
    "white chocolate": "chocolate chips",
    "chocolate": "chocolate chips",
    "cayenne": "cayenne pepper",
    "smoked chile": "chili powder",
    "ancho chile": "chili powder",
    "guajillo": "chili powder",
    "pasilla": "chili powder",
    "chinese five spice": "allspice",
    "five spice": "allspice",
    "ras el hanout": "garam masala",
    "harissa": "chili powder",
    "tahini": "almond butter",
    "stuffing mix": "breadcrumbs",
    "cracker crumbs": "breadcrumbs",
    "graham crackers": "breadcrumbs",
    "biscuit": "white bread",
    "biscuits": "white bread",
    "scones": "white bread",
    "muffin": "white bread",
    "muffins": "white bread",
    "rolls": "white bread",
    "tortilla": "flour tortilla",
    "tortillas": "flour tortilla",
    "wrap": "flour tortilla",
    "wraps": "flour tortilla",
    "tomato paste": "tomato",
    "tomato puree": "tomato",
    "tomato sauce": "tomato",
    "diced tomatoes": "tomato",
    "crushed tomatoes": "tomato",
    "stewed tomatoes": "tomato",

    # Round 4: long-tail synonyms (most common remaining unmatched)
    "creme fraiche": "sour cream",
    "creme": "heavy cream",
    "jaggery": "brown sugar",
    "demerara sugar": "brown sugar",
    "muscovado": "brown sugar",
    "palm sugar": "brown sugar",
    "coconut sugar": "brown sugar",
    "urad dal": "lentils",
    "toor dal": "lentils",
    "chana dal": "lentils",
    "moong dal": "lentils",
    "dal": "lentils",
    "parsnip": "carrots",
    "parsnips": "carrots",
    "red chile flakes": "cayenne pepper",
    "chile flakes": "cayenne pepper",
    "red pepper flakes": "cayenne pepper",
    "crushed red pepper": "cayenne pepper",
    "red chillis": "green chiles",
    "red chilli": "green chiles",
    "fruit": "apple",
    "fruits": "apple",
    "mixed fruit": "apple",
    "berries": "blueberries",
    "mixed berries": "blueberries",
    "catsup": "ketchup",
    "jeera": "cumin",
    "jeera powder": "cumin",
    "marzipan": "almond flour",
    "snail": "shrimp",
    "snails": "shrimp",
    "escargot": "shrimp",
    "rib rub": "allspice",
    "bbq rub": "allspice",
    "dry rub": "allspice",
    "spice rub": "allspice",
    "fat": "butter",
    "cooking fat": "butter",
    "shortening fat": "shortening",
    "sweetener": "sugar",
    "artificial sweetener": "sugar",
    "alubosa": "onion",
    "tatase": "green chiles",
    "tatashe": "green chiles",
    "duck": "chicken breast",
    "duck breast": "chicken breast",
    "duck breasts": "chicken breast",
    "ea duck breasts": "chicken breast",
    "tripe": "ponmo",
    "pancetta": "ground beef 90% lean",
    "bacon": "ground beef 90% lean",
    "salami": "ground beef 90% lean",
    "haddock": "cod",
    "trout": "cod",
    "flounder": "cod",
    "halibut": "cod",
    "mackerel": "cod",
    "anchovies": "cod",
    "sardines": "cod",
    "xanthan gum": "cornstarch",
    "guar gum": "cornstarch",
    "agar": "cornstarch",
    "msg": "salt",
    "monosodium glutamate": "salt",
    "sauerkraut": "cabbage",
    "suet": "lard",
    "matzo meal": "breadcrumbs",
    "matzah meal": "breadcrumbs",
    "panko": "breadcrumbs",
    "maize": "corn",
    "sweet corn": "corn",
    "creamed corn": "corn",
    "croutons": "white bread",
    "salad": "lettuce",
    "green salad": "lettuce",
    "caesar salad": "lettuce",
    "mixed greens": "lettuce",
    "arugula": "lettuce",
    "rocket": "lettuce",
    "watercress": "lettuce",
    "endive": "lettuce",
    "radicchio": "lettuce",
    "sirloin": "sirloin steak",
    "top sirloin": "sirloin steak",
    "ribeye": "sirloin steak",
    "tenderloin": "sirloin steak",
    "half half": "half-and-half",
    "pumpkin": "pumpkin",
    "ewedu leaves": "spinach",
    "ewedu": "spinach",
    "waterleaf": "spinach",
    "bitter leaf": "kale",
    "ehu seeds": "fenugreek seeds",
    "citric acid": "lemon juice",
    "lemon zest": "lemon juice",
    "lime zest": "lime juice",
    "orange zest": "orange",
    "instant coffee": "cocoa powder",
    "instant coffee powder": "cocoa powder",
    "coffee": "cocoa powder",
    "espresso": "cocoa powder",
    "matcha": "cocoa powder",
    "tea": "cocoa powder",
    "milk powder": "skim milk",
    "powdered milk": "skim milk",
    "dry milk": "skim milk",
    "evaporated milk": "whole milk",
    "condensed milk": "whole milk",
    "coconut milk": "whole milk",
    "coconut cream": "heavy cream",
    "coconut water": "water",
    "coconut": "coconut oil",
    "shredded coconut": "coconut oil",
    "desiccated coconut": "coconut oil",
    "raisin": "raisins",
    "dates": "raisins",
    "date": "raisins",
    "fig": "raisins",
    "figs": "raisins",
    "prunes": "raisins",
    "cranberries": "raisins",
    "apricots": "raisins",
    "dried apricots": "raisins",
    "dried fruit": "raisins",
    "kiwi": "strawberries",
    "mango": "strawberries",
    "papaya": "strawberries",
    "pineapple": "strawberries",
    "watermelon": "strawberries",
    "cantaloupe": "strawberries",
    "honeydew": "strawberries",
    "grape": "strawberries",
    "grapes": "strawberries",
    "pear": "apple",
    "pears": "apple",
    "plum": "apple",
    "plums": "apple",
    "cherry": "strawberries",
    "cherries": "strawberries",
    "lime": "lime juice",
    "lemon": "lemon juice",
    "lemons": "lemon juice",
    "limes": "lime juice",
}

# Additional descriptors stripped during normalization (round 2).
EXTRA_DESCRIPTORS = {
    "boneless", "skinless", "boned", "skin", "bone-in", "bone",
    "trimmed", "lean", "extra-lean", "lean-trimmed",
    "smoked", "unsmoked", "salted", "unsalted",
    "dry", "dried", "wet", "wetted",
    "fine", "finely", "coarse", "coarsely", "thinly", "thickly",
    "premium", "best", "quality", "good",
}


# Normalize synonym targets to match the normalized food keys (strips parens
# in food names like "Black Beans (cooked)" → "black beans").
def _normalize_synonyms():
    return {
        k.strip().lower(): re.sub(r"\s+", " ", re.sub(r"\([^)]*\)", "", v.lower())).strip()
        for k, v in SYNONYMS.items()
    }


SYNONYMS = _normalize_synonyms()


def _strip_plural_each_word(s: str) -> str:
    """Strip a trailing 's' from each word (naive singularization)."""
    return re.sub(r"\b(\w{3,}?)s\b", r"\1", s)


def match_food(phrase: str, foods, food_by_key, usda_data=None, usda_inv=None):
    """Find a food match for the ingredient phrase using:
      1. exact key match (after normalization)
      2. synonym mapping
      3. longest whole-word substring match against food keys
      4. plural-stripped retry of #1-#3
      5. USDA SR Legacy token-overlap match (~7,500 foods) — final fallback
    Returns the food dict or None."""
    norm = normalize_ingredient_phrase(phrase)
    if not norm:
        return None

    def _lookup(text):
        if text in food_by_key:
            return food_by_key[text]
        if text in SYNONYMS:
            target = SYNONYMS[text]
            if target in food_by_key:
                return food_by_key[target]
            for f in foods:
                if target in f["key"]:
                    return f
        # Longest whole-word substring match
        best = None
        best_len = 0
        for f in foods:
            key = f["key"]
            if len(key) <= best_len:
                continue
            if re.search(r"\b" + re.escape(key) + r"\b", text):
                best = f
                best_len = len(key)
        # Also try synonyms whose key appears as a whole word in text
        for syn_key, target in SYNONYMS.items():
            if len(syn_key) <= best_len:
                continue
            if re.search(r"\b" + re.escape(syn_key) + r"\b", text):
                f = food_by_key.get(target)
                if not f:
                    for ff in foods:
                        if target in ff["key"]:
                            f = ff
                            break
                if f:
                    best = f
                    best_len = len(syn_key)
        return best

    found = _lookup(norm)
    if found:
        return found
    # Plural-strip and retry
    singular = _strip_plural_each_word(norm)
    if singular != norm:
        found = _lookup(singular)
        if found:
            return found
    # Final fallback: USDA SR Legacy. Try min_score 2 first (high-confidence
    # multi-token matches), then drop to 1 (single-token matches like "pumpkin"
    # or "haddock" that USDA has but only share one query token).
    found = usda_match(phrase, usda_data, usda_inv, min_score=2)
    if found:
        return found
    return usda_match(phrase, usda_data, usda_inv, min_score=1)


# ---------- Quantity → grams (density-aware) ----------

_LABEL_PARSE_RE = re.compile(
    r"^\s*"
    r"(\d+(?:\.\d+)?|\d+\s*/\s*\d+)"   # count
    r"\s*([A-Za-z]+)"                   # unit word
)


def _parse_default_serving_label(food):
    """Return (count, unit_normalized) from DefaultServingLabel, or (None, None)."""
    label = (food.get("default_serving_label") or "").strip().lower()
    m = _LABEL_PARSE_RE.match(label)
    if not m:
        return None, None
    cnt_str, unit_str = m.groups()
    if "/" in cnt_str:
        num, den = cnt_str.split("/")
        try:
            count = float(num) / float(den) if float(den) else None
        except ValueError:
            count = None
    else:
        try:
            count = float(cnt_str)
        except ValueError:
            count = None
    if not count or count <= 0:
        return None, None
    return count, unit_str.rstrip("s.,;")


# Memoized density (g/ml) per food id.
_density_cache = {}


def food_density_g_per_ml(food):
    """Compute g/ml for this food using its DefaultServingLabel/Size, or None."""
    fid = id(food)
    if fid in _density_cache:
        return _density_cache[fid]

    count, unit = _parse_default_serving_label(food)
    size_g = food.get("default_serving_g") or 0
    density = None
    if count and unit and size_g > 0:
        ml_per_label_unit = VOLUME_UNITS_ML.get(unit)
        if ml_per_label_unit:
            total_ml = count * ml_per_label_unit
            if total_ml > 0:
                density = size_g / total_ml
    _density_cache[fid] = density
    return density


def food_grams_per_whole(food):
    """For "1 X" units (egg, slice, breast, etc.) where unit is the food's
    own default label. Returns grams per single piece, or None."""
    count, unit = _parse_default_serving_label(food)
    size_g = food.get("default_serving_g") or 0
    if count and size_g > 0 and unit not in VOLUME_UNITS_ML and unit not in WEIGHT_UNITS_G:
        return size_g / count
    return None


def quantity_to_grams(qty, qty_upper, unit, food):
    """Best-effort grams. qty may be None (no number given)."""
    if qty is None:
        return 0.0
    if qty_upper is not None:
        qty = (qty + qty_upper) / 2.0

    if unit:
        u = unit.lower()
        if u in WEIGHT_UNITS_G:
            return qty * WEIGHT_UNITS_G[u]
        if u in FIXED_SMALL_UNITS_G:
            return qty * FIXED_SMALL_UNITS_G[u]
        if u in VOLUME_UNITS_ML:
            ml = qty * VOLUME_UNITS_ML[u]
            density = food_density_g_per_ml(food) if food else None
            if density is None:
                # Heuristic: foods with default_serving_g <= 30 and a small-unit
                # label (tsp/tbsp/oz) are usually dense seasonings/oils — use
                # ~0.9. Everything else (vegetables/produce) default closer to
                # water density.
                density = WATER_DENSITY_G_PER_ML
            return ml * density
        # "clove" / "stick" / "slice" — use food's per-piece weight if it
        # matches the food's own label unit; else fall back to a fixed estimate.
        if u in {"clove", "cloves"}:
            per_piece = food_grams_per_whole(food) if food else None
            return qty * (per_piece if per_piece else 3.0)
        if u in {"stick", "sticks"}:
            return qty * 113.0   # butter convention
        if u in {"slice", "slices"}:
            per_piece = food_grams_per_whole(food) if food else None
            return qty * (per_piece if per_piece else 28.0)

    # No unit: count = whole pieces of the food
    if food and food.get("default_serving_g"):
        return qty * food["default_serving_g"]
    return qty * 100.0


# ---------- Servings parsing ----------

def parse_servings(s):
    if not s:
        return None
    t = s.strip().lower()
    # "4-6" or "4 to 6"
    m = re.search(r"(\d+)\s*(?:-|–|to)\s*(\d+)", t)
    if m:
        return (float(m.group(1)) + float(m.group(2))) / 2.0
    m = re.search(r"(\d+(?:\.\d+)?)", t)
    if m:
        return float(m.group(1))
    return None


# ---------- DB + main flow ----------

def get_conn():
    cs = os.environ.get("RECIPEDB_SQL_CONNECTION", "")
    if not cs:
        sys.exit("RECIPEDB_SQL_CONNECTION env var is not set.")
    return pyodbc.connect(cs, autocommit=False)


def main():
    args = set(sys.argv[1:])
    do_reload = "--reload" in args

    print("Loading Foods library...")
    foods = load_foods()
    food_by_key = {f["key"]: f for f in foods}
    print(f"  {len(foods)} foods loaded")

    print("Loading USDA SR Legacy fallback...")
    usda_data, usda_inv = load_usda()
    if usda_data:
        enriched = enrich_foods_from_usda(foods, usda_data, usda_inv)
        print(f"  enriched {enriched}/{len(foods)} SeedData foods with USDA's "
              f"sodium / cholesterol / sat fat / sugar")

    conn = get_conn()
    cur = conn.cursor()

    if do_reload:
        print("Wiping WIKIBOOKS_RecipeNutrition...")
        cur.execute("DELETE FROM WIKIBOOKS_RecipeNutrition")
        conn.commit()

    print("Loading recipes + ingredients from SQL...")
    cur.execute(
        "SELECT WikibooksRecipeID, RecipeName, Servings FROM WIKIBOOKS_Recipes "
        "WHERE IsExcluded = 0"
    )
    recipes = cur.fetchall()
    print(f"  {len(recipes)} recipes")

    # Pull all ingredients in one query and bucket them
    cur.execute(
        "SELECT WikibooksRecipeID, Description FROM WIKIBOOKS_RecipeIngredients "
        "ORDER BY WikibooksRecipeID, SortOrder"
    )
    ing_by_recipe = {}
    for rid, desc in cur.fetchall():
        ing_by_recipe.setdefault(rid, []).append(desc)

    # For idempotency, get existing nutrition rows so we can update vs insert.
    cur.execute("SELECT WikibooksRecipeID FROM WIKIBOOKS_RecipeNutrition")
    existing = {row[0] for row in cur.fetchall()}

    unmatched_counter = Counter()
    match_rate_bands = {"0-25": 0, "25-50": 0, "50-75": 0, "75-100": 0}
    no_servings = 0
    processed = 0

    for rid, name, servings_raw in recipes:
        ings = ing_by_recipe.get(rid, [])
        if not ings:
            continue
        servings = parse_servings(servings_raw)
        if servings is None:
            no_servings += 1
            divisor = 1.0
            serving_note = "Total recipe (servings unknown)"
        else:
            divisor = servings
            serving_note = f"Per serving (of {servings_raw.strip()})"

        total_cal = total_pro = total_carb = total_fat = total_fib = 0.0
        total_sodium = total_chol = total_satfat = total_sugar = 0.0
        matched = 0
        for raw in ings:
            qty, qty_up, unit, phrase = parse_ingredient(raw)
            food = match_food(phrase, foods, food_by_key, usda_data, usda_inv)
            if food is None:
                norm = normalize_ingredient_phrase(phrase)
                if norm:
                    unmatched_counter[norm] += 1
                continue
            matched += 1
            grams = quantity_to_grams(qty, qty_up, unit, food)
            scale = grams / 100.0
            total_cal    += food["cal"]         * scale
            total_pro    += food["protein"]     * scale
            total_carb   += food["carb"]        * scale
            total_fat    += food["fat"]         * scale
            total_fib    += food["fiber"]       * scale
            total_sodium += food.get("sodium", 0.0)      * scale
            total_chol   += food.get("cholesterol", 0.0) * scale
            total_satfat += food.get("satfat", 0.0)      * scale
            total_sugar  += food.get("sugar", 0.0)       * scale

        match_rate = (matched / len(ings)) * 100.0 if ings else 0.0
        if   match_rate < 25:  match_rate_bands["0-25"]   += 1
        elif match_rate < 50:  match_rate_bands["25-50"]  += 1
        elif match_rate < 75:  match_rate_bands["50-75"]  += 1
        else:                  match_rate_bands["75-100"] += 1

        cps    = int(round(total_cal / divisor))
        pro    = round(total_pro    / divisor, 2)
        carb   = round(total_carb   / divisor, 2)
        fat    = round(total_fat    / divisor, 2)
        fib    = round(total_fib    / divisor, 2)
        sodium = round(total_sodium / divisor, 2)
        chol   = round(total_chol   / divisor, 2)
        satfat = round(total_satfat / divisor, 2)
        sugar  = round(total_sugar  / divisor, 2)

        if rid in existing:
            cur.execute(
                """UPDATE WIKIBOOKS_RecipeNutrition
                   SET CaloriesPerServing=?, ProteinGrams=?, TotalCarbsGrams=?,
                       TotalFatGrams=?, FiberGrams=?, SodiumMg=?, CholesterolMg=?,
                       SaturatedFatGrams=?, SugarGrams=?, ServingSizeNote=?,
                       IngredientMatchRate=?, DateComputed=GETDATE()
                   WHERE WikibooksRecipeID=?""",
                (cps, pro, carb, fat, fib, sodium, chol, satfat, sugar,
                 serving_note, round(match_rate, 2), rid),
            )
        else:
            cur.execute(
                """INSERT INTO WIKIBOOKS_RecipeNutrition
                   (WikibooksRecipeID, CaloriesPerServing, ProteinGrams,
                    TotalCarbsGrams, TotalFatGrams, FiberGrams,
                    SodiumMg, CholesterolMg, SaturatedFatGrams, SugarGrams,
                    ServingSizeNote, IngredientMatchRate, DateComputed)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, GETDATE())""",
                (rid, cps, pro, carb, fat, fib, sodium, chol, satfat, sugar,
                 serving_note, round(match_rate, 2)),
            )

        processed += 1
        if processed % 500 == 0:
            conn.commit()
            print(f"  ...{processed} processed")

    conn.commit()

    print(f"\nDONE — {processed} recipes processed")
    print(f"  recipes with unparseable Servings (defaulted to 1): {no_servings}")
    print("\nMatch-rate distribution:")
    for band, count in match_rate_bands.items():
        pct = 100 * count / max(processed, 1)
        print(f"  {band:>6}%  : {count:>5} ({pct:.1f}%)")

    print("\nTop 50 UNMATCHED ingredient phrases (frequency):")
    for phrase, count in unmatched_counter.most_common(50):
        print(f"  {count:>5}  {phrase}")

    print(f"\nTotal unique unmatched phrases: {len(unmatched_counter):,}")
    print("(Add the high-frequency ones to SeedData.Foods.cs to improve coverage.)")


if __name__ == "__main__":
    main()

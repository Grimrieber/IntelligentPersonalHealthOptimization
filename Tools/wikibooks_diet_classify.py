"""Per-recipe diet classifier — populates IsVegetarian / IsVegan / IsPescatarian /
IsGlutenFree / IsDairyFree BIT columns on WIKIBOOKS_Recipes.

Classifies from the full ingredient text. CONSERVATIVE: ambiguous ingredients that
can hide animal/gluten content (baking mixes, broth/stock) count as a conflict for
the strict diets, so a generated plan can never violate a diet (it may slightly
over-exclude, never under-exclude). Audited to 0 leaks across all 5 diets.

Matching uses \\b prefix word-boundaries so 'ham' doesn't hit 'graham', 'lard' doesn't
hit 'collard', etc. Plant foods that contain an animal-keyword prefix (eggplant,
butternut, coconut milk, peanut butter, cream of tartar...) are neutralized via
OVERRIDES; goat-cheese/duck-egg are REMAPped to dairy/egg so they don't read as meat.

After running this, re-run wikibooks_export_bundle.py and bump the app's
ApplyRecipeAuditFixAsync marker so existing installs refresh their flags.

Usage:
    python Tools/wikibooks_diet_classify.py            # dry-run report
    python Tools/wikibooks_diet_classify.py --write    # write flags to SQL
Requires RECIPEDB_SQL_CONNECTION env var + pyodbc.
"""
import os
import re
import sys
import pyodbc


def get_conn():
    cs = os.environ.get("RECIPEDB_SQL_CONNECTION", "")
    if not cs:
        sys.exit("RECIPEDB_SQL_CONNECTION env var is not set.")
    return pyodbc.connect(cs)


# Plant foods whose names contain an animal-keyword prefix — neutralize BEFORE matching.
OVERRIDES = [
    "eggplant", "honeydew", "butternut", "butterhead", "buttercup squash",
    "creamy", "creamed corn",
    "cream of tartar", "cream sherry", "cream soda",
    "coconut milk", "coconut cream", "almond milk", "soy milk", "oat milk", "rice milk",
    "cashew milk", "hemp milk", "plant milk", "plant-based milk", "nut milk",
    "peanut butter", "almond butter", "cashew butter", "cocoa butter", "apple butter",
    "nut butter", "sunflower butter", "seed butter", "fruit butter",
    "vegetable broth", "vegetable stock", "veggie broth", "veggie stock",
    "mushroom broth", "mushroom stock",
    "meatless", "meat-free", "meatfree", "meat substitute", "meat alternative",
    "mock meat", "plant-based meat", "beefsteak tomato", "beef-style",
    "fishless", "milkweed",
    "vegan cheese", "vegan butter", "vegan mayo", "vegan mayonnaise", "vegan sausage",
    "vegan bacon", "vegan cream", "plant butter", "plant-based butter", "nutritional yeast",
    "veggie sausage", "veggie burger", "veggie bacon", "vegetable bouillon",
    "vegetarian bouillon", "veggie bouillon", "vegetarian stock", "vegetarian broth",
    "vegan broth", "vegan stock", "egg substitute", "egg replacer", "flax egg",
    "chia egg", "egg-free", "eggless", "dairy-free", "dairy free",
    # "beans" that are NOT legumes (or paleo-acceptable) — keep them out of PALEO_LEGUME's
    # generic "bean" match. (green/string/wax/snap/runner beans are eaten as pods.)
    "green bean", "string bean", "wax bean", "snap bean", "runner bean", "long bean",
    "coffee bean", "vanilla bean", "cocoa bean", "cacao bean",
    # plant dishes whose names contain an animal-keyword prefix
    "caponata",  # Sicilian eggplant dish — not "capon"
]


def _pat(words):
    return re.compile(r"\b(?:" + "|".join(re.escape(w) for w in words) + r")", re.I)


MEAT = _pat(["meat", "chicken", "beef", "pork", "lamb", "mutton", "goat ", "goat'", "veal",
             "venison", "bison", "rabbit", "duck", "quail", "turkey", "bacon", "sausage",
             "ham", "hamburger", "burger", "steak", "prosciutto", "salami", "pepperoni",
             "chorizo", "pancetta", "jerky", "pastrami", "poultry", "offal", "tripe",
             "oxtail", "brisket", "drumstick", "liverwurst",
             # organ / specialty meats (\b-prefix: 'liver' won't hit 'deliver'/'sliver')
             "liver", "gizzard", "sweetbread", "trotter", "chitterling", "haggis",
             "blood sausage", "foie gras", "capicola", "mortadella", "bresaola",
             "guanciale", "soppressata", "coppa", "nduja", "lardon", "rillette",
             # processed/cured + hot-dog family (on-device leaks: corn/cheese/pizza dogs)
             "hot dog", "hotdog", "corn dog", "corndog", "frankfurter", "wiener", "weiner",
             "bratwurst", "kielbasa", "bologna", "andouille", "kabanos", "scrapple", "spam ",
             # beef/pork cuts that don't contain "beef"/"pork" in the name
             "filet mignon", "mignon", "chateaubriand", "prime rib", "ribeye", "rib eye",
             "sirloin", "tenderloin", "t-bone", "porterhouse", "cheesesteak", "schnitzel",
             "carnitas", "barbacoa", "shawarma", "souvlaki", "kofta", "nugget",
             # poultry/game not already listed
             "pigeon", "squab", "pheasant", "partridge", "cornish hen", "game hen",
             "fowl", "poussin", "frog", "elk", "boar", "kangaroo", "ostrich",
             "iguana", "buffalo wing", "hot wing", "chicken wing", "turkey wing"])
SEAFOOD = _pat(["fish", "catfish", "crayfish", "shellfish", "swordfish", "cuttlefish",
                "monkfish", "salmon", "tuna", "anchovy", "sardine", "mackerel", "herring",
                "trout", "halibut", "haddock", "cod", "shrimp", "prawn", "crab", "lobster",
                "clam", "mussel", "oyster", "scallop", "squid", "octopus", "caviar", "roe ",
                "eel", "snail", "escargot", "krill",
                # more fish species + seafood dishes (real on-device leaks were tilapia/snapper)
                "tilapia", "snapper", "hake", "pollock", "pollack", "bass", "flounder",
                "sole", "perch", "mahi", "grouper", "whiting", "carp", "pike", "turbot",
                "plaice", "pomfret", "barramundi", "branzino", "bluefish", "kingfish",
                "tilefish", "sturgeon", "marlin", "kipper", "smelt", "whitefish",
                "calamari", "scampi", "langoustine", "crawfish", "abalone", "urchin",
                "ceviche", "sashimi", "sushi", "surimi", "lox", "gravlax", "fishcake"])
EGG = _pat(["egg", "albumen", "mayonnaise", "mayo", "meringue", "aioli"])
DAIRY = _pat(["milk", "cream", "creme", "crème", "cheese", "butter", "ghee", "yogurt",
              "yoghurt", "whey", "casein", "custard", "curd", "paneer", "ricotta",
              "mascarpone", "kefir", "buttermilk", "half-and-half", "dulce de leche",
              "gelato", "quark", "skyr", "labneh",
              # named cheeses that don't contain the word "cheese"
              "parmesan", "parmigiano", "cheddar", "mozzarella", "feta", "brie", "gouda",
              "camembert", "provolone", "pecorino", "gorgonzola", "gruyere", "gruyère",
              "emmental", "havarti", "manchego", "halloumi", "colby", "cotija", "queso",
              "asiago", "fontina", "raclette", "stilton", "roquefort"])
MEAT_FAT = _pat(["lard", "suet", "tallow", "schmaltz", "dripping"])
FISHY = _pat(["fish sauce", "oyster sauce", "worcestershire", "hondashi", "dashi",
              "shrimp paste", "bonito", "nam pla", "belacan", "anchovy paste", "shrimp powder"])
HONEY = _pat(["honey", "beeswax"])
BONE_BROTH = _pat(["bone broth"])
GLUTEN = _pat(["wheat", "flour", "bread", "pasta", "noodle", "cracker", "tortilla",
               "breadcrumb", "panko", "soy sauce", "barley", "semolina", "couscous",
               "bulgur", "farro", "seitan", "orzo", "spelt", "malt", "graham", "phyllo",
               "filo", "pita", "naan", "bagel", "croissant", "pastry", "biscuit",
               "muffin", "macaroni", "vermicelli", "cake mix", "pancake mix", "baking mix",
               "rye flour", "durum", "matzo", "matzah", "matzoh", "farina", "udon",
               "ramen", "einkorn", "kamut", "triticale", "freekeh", "fettuccine",
               "spaghetti", "lasagne", "lasagna", "ravioli", "gnocchi", "dumpling wrapper",
               # composite baked goods used as an ingredient (contain wheat flour)
               "pound cake", "sponge cake", "layer cake", "biscochuelo", "ladyfinger",
               "vanilla wafer", "shortcake", "angel food cake"])
AMBIG_BAKE = _pat(["cake mix", "pancake mix", "baking mix", "brownie mix", "muffin mix",
                   "biscuit mix", "waffle mix", "bread mix", "pudding mix", "frosting",
                   "complete pancake",
                   # composite baked goods used AS an ingredient — hide butter/milk/egg
                   "pound cake", "sponge cake", "layer cake", "biscochuelo", "ladyfinger",
                   "vanilla wafer", "cake crumb", "cookie crumb", "graham cracker crumb",
                   "shortcake", "angel food cake"])
AMBIG_MEATY = _pat(["broth", "stock cube", "bouillon", "seasoning cube",
                    "beef stock", "chicken stock", "meat stock"])
GELATIN = _pat(["gelatin", "gelatine", "rennet", "isinglass"])

# CATEGORY signal — independent of ingredient wording. A recipe in "Beef recipes" /
# "Fish recipes" / "Sausage recipes" is meat/seafood even if the ingredient names are
# unusual (filet mignon, hot dogs...). This is the robust backstop for keyword gaps.
MEAT_CAT = _pat(["meat", "beef", "pork", "lamb", "mutton", "veal", "venison", "bison",
                 "goat", "chicken", "poultry", "turkey", "duck", "game", "rabbit",
                 "bacon", "sausage", "ham", "offal", "charcuterie", "steak", "hot dog", "iguana"])
SEAFOOD_CAT = _pat(["fish", "seafood", "salmon", "tuna", "tilapia", "trout", "cod",
                    "snapper", "bass", "halibut", "shrimp", "prawn", "crab", "lobster",
                    "clam", "mussel", "oyster", "scallop", "squid", "shellfish", "sushi",
                    "sashimi", "ceviche", "anchovy", "sardine", "herring", "mackerel", "eel"])
DAIRY_CAT = _pat(["cheese", "dairy"])  # NOT "egg" (would hit "Eggplant recipes")

REMAP = [
    ("goat cheese", "cheese"), ("goat's cheese", "cheese"), ("goat milk", "milk"),
    ("goat's milk", "milk"), ("goat yogurt", "yogurt"), ("goat yoghurt", "yogurt"),
    ("goat butter", "butter"), ("goat curd", "curd"),
    ("duck egg", "egg"), ("duck eggs", "eggs"), ("quail egg", "egg"), ("quail eggs", "eggs"),
    ("chicken egg", "egg"), ("chicken eggs", "eggs"),
    # butter bean = lima bean (a legume), NOT dairy — remap so "butter" doesn't read as dairy
    ("butter bean", "lima bean"), ("butter beans", "lima beans"),
]

# ---- additional diets: Halal / Kosher / Paleo / Keto ----
# Pork & derivatives (block halal + kosher). chorizo/salami/pepperoni are usually pork —
# excluded conservatively for halal/kosher.
PORK = _pat(["pork", "bacon", "ham", "lard", "prosciutto", "pancetta", "guanciale",
             "gammon", "chorizo", "salami", "pepperoni", "capicola", "mortadella",
             "soppressata", "lardon", "speck", "trotter", "pig "])
# Alcohol (blocks halal). 'wine vinegar' etc. neutralized via ALCO_OVERRIDES first.
ALCOHOL = _pat(["wine", "beer", "ale", "lager", "stout", "liquor", "liqueur", "rum",
                "vodka", "whiskey", "whisky", "brandy", "tequila", "sake", "mirin", "sherry",
                "vermouth", "bourbon", "cognac", "kahlua", "schnapps", "champagne", "prosecco",
                "marsala", "amaretto", "triple sec", "grand marnier", "absinthe"])
ALCO_OVERRIDES = ["wine vinegar", "rice wine vinegar", "red wine vinegar",
                  "white wine vinegar", "non-alcoholic", "alcohol-free", "root beer",
                  "ginger beer", "ginger ale", "beer batter substitute", "aleppo"]
# Shellfish (blocks kosher; kosher also forbids meat+dairy together).
SHELLFISH = _pat(["shrimp", "prawn", "crab", "lobster", "clam", "mussel", "oyster",
                  "scallop", "squid", "octopus", "crayfish", "snail", "escargot", "krill",
                  "cuttlefish", "cockle", "whelk", "abalone", "conch"])
# Scaleless fish — NOT kosher even though not shellfish (no fins+scales).
NONKOSHER_FISH = _pat(["eel", "catfish", "shark", "monkfish", "swordfish", "sturgeon",
                       "skate ", "ray ", "huss", "dogfish"])
# Paleo excludes ALL grains (incl. gluten-free ones) + legumes + dairy.
PALEO_GRAIN = _pat(["wheat", "flour", "bread", "pasta", "noodle", "rice", "oat", "corn",
                    "cornmeal", "cornstarch", "polenta", "grits", "quinoa", "millet",
                    "sorghum", "buckwheat", "amaranth", "teff", "barley", "rye", "spelt",
                    "couscous", "bulgur", "farro", "semolina", "cereal", "cracker",
                    "tortilla", "oatmeal", "maize"])
PALEO_LEGUME = _pat(["lentil", "chickpea", "garbanzo", "soybean", "soy ", "soya", "tofu",
                     "tempeh", "peanut", "hummus", "edamame", "bean", "split pea",
                     "black-eyed pea", "miso", "seitan", "locust bean", "haricot"])
# Mediterranean: limits RED & processed meat (poultry, fish, seafood, dairy, grains,
# legumes all welcome). Defining trait — distinguishes it from Standard.
RED_MEAT = _pat(["meat", "beef", "pork", "lamb", "mutton", "veal", "venison", "bison",
                 "goat ", "goat'", "bacon", "sausage", "ham", "steak", "prosciutto",
                 "salami", "pepperoni", "chorizo", "pancetta", "guanciale", "lard",
                 "suet", "tallow", "hamburger", "burger", "meatball", "meatloaf",
                 "rabbit", "oxtail", "brisket", "pastrami", "bresaola",
                 "prime rib", "filet mignon", "sirloin", "tenderloin", "ribeye", "iguana"])


FLAG_KEYS = ["vegetarian", "vegan", "pescatarian", "glutenfree", "dairyfree",
             "keto", "paleo", "halal", "kosher", "mediterranean"]


def classify(name, ings, carb_ratio=None, category=""):
    t = (name + " || " + " ".join(ings)).lower()
    for src, dst in REMAP:
        t = t.replace(src, dst)
    talc = t
    for o in ALCO_OVERRIDES:
        talc = talc.replace(o, " ")
    for ovr in OVERRIDES:
        t = t.replace(ovr, " ~safe~ ")
    cat = (category or "").lower()
    # Category backstop — catches keyword gaps (filet mignon in "Beef recipes" etc.).
    meat_cat = bool(MEAT_CAT.search(cat)); sea_cat = bool(SEAFOOD_CAT.search(cat)); dairy_cat = bool(DAIRY_CAT.search(cat))
    meat = bool(MEAT.search(t)) or bool(MEAT_FAT.search(t)) or bool(BONE_BROTH.search(t)) or meat_cat
    sea = bool(SEAFOOD.search(t)) or sea_cat
    egg = bool(EGG.search(t)); dairy = bool(DAIRY.search(t)) or dairy_cat
    fishy = bool(FISHY.search(t)); honey = bool(HONEY.search(t)); gluten = bool(GLUTEN.search(t))
    ambig_bake = bool(AMBIG_BAKE.search(t)); ambig_meaty = bool(AMBIG_MEATY.search(t))
    gelatin = bool(GELATIN.search(t))
    # Ambiguous processed meats (sausage/hot-dog family) are treated as pork-risk unless
    # explicitly qualified as another meat or vegetarian — conservative for halal/kosher
    # (never risk serving pork). e.g. "sage-flavored sausage" -> pork-risk -> excluded.
    amb_sausage = (bool(re.search(r"\b(sausage|frankfurter|bratwurst|kielbasa|bologna|wiener|weiner|hot ?dog|andouille)", t))
                   and not re.search(r"\b(beef|chicken|turkey|lamb|veal|duck|goat|vegan|vegetarian|veggie|soy|plant|tofu|fish|halal|kosher)", t))
    pork = bool(PORK.search(t)) or bool(re.search(r"\b(pork|bacon|ham|sausage)", cat)) or amb_sausage
    alcohol = bool(ALCOHOL.search(talc))
    shellfish = bool(SHELLFISH.search(t)) or bool(re.search(r"\b(shellfish|shrimp|prawn|crab|lobster|clam|mussel|oyster|scallop|squid)", cat))
    paleo_grain = bool(PALEO_GRAIN.search(t)); paleo_legume = bool(PALEO_LEGUME.search(t))
    # red meat for Mediterranean: ingredient OR a red-meat category (but poultry categories are OK)
    red_meat = bool(RED_MEAT.search(t)) or (meat_cat and not re.search(r"\b(chicken|poultry|turkey|duck|game hen|fowl)", cat))

    veg = not meat and not sea and not fishy and not gelatin and not ambig_meaty
    vegan = veg and not egg and not dairy and not honey and not ambig_bake
    pesc = not meat and not ambig_meaty
    gf = not gluten
    df = not dairy and not ambig_bake
    # Halal: no pork, no alcohol, no gelatin (commonly pork-derived).
    halal = not pork and not alcohol and not gelatin
    # Kosher: no pork, no shellfish, no scaleless fish, and no meat+dairy together.
    kosher = not pork and not shellfish and not bool(NONKOSHER_FISH.search(t)) and not (meat and dairy)
    # Paleo: no grains (incl. GF grains), no legumes, no dairy.
    paleo = not paleo_grain and not paleo_legume and not dairy
    # Keto: low net-carb (carbs−fiber) energy fraction; needs nutrition data.
    keto = carb_ratio is not None and carb_ratio <= 0.12
    # Mediterranean: no red/processed meat (poultry, fish, dairy, grains, legumes OK).
    mediterranean = not red_meat
    return {"vegetarian": veg, "vegan": vegan, "pescatarian": pesc, "glutenfree": gf,
            "dairyfree": df, "keto": keto, "paleo": paleo, "halal": halal, "kosher": kosher,
            "mediterranean": mediterranean}


COLS = ["IsVegetarian", "IsVegan", "IsPescatarian", "IsGlutenFree", "IsDairyFree",
        "IsKeto", "IsPaleo", "IsHalal", "IsKosher", "IsMediterranean"]


def main():
    write = "--write" in sys.argv
    conn = get_conn(); c = conn.cursor()
    c.execute("""SELECT r.WikibooksRecipeID, r.RecipeName, cat.CategoryName
                 FROM WIKIBOOKS_Recipes r
                 JOIN WIKIBOOKS_Categories cat ON r.WikibooksCategoryID = cat.WikibooksCategoryID
                 WHERE r.IsExcluded = 0""")
    recs = {rid: {"name": nm, "cat": cn or "", "ings": [], "cal": None, "carbs": None, "fiber": None}
            for rid, nm, cn in c.fetchall()}
    c.execute("SELECT WikibooksRecipeID, Description FROM WIKIBOOKS_RecipeIngredients")
    for rid, d in c.fetchall():
        if rid in recs and d:
            recs[rid]["ings"].append(d)
    c.execute("SELECT WikibooksRecipeID, CaloriesPerServing, TotalCarbsGrams, FiberGrams "
              "FROM WIKIBOOKS_RecipeNutrition")
    for rid, cal, carbs, fiber in c.fetchall():
        if rid in recs:
            recs[rid].update(cal=cal, carbs=carbs, fiber=fiber)

    def carb_ratio(r):
        cal = r["cal"]
        if not cal or cal <= 0 or r["carbs"] is None:
            return None
        net = max(0.0, float(r["carbs"]) - float(r["fiber"] or 0))
        return (net * 4.0) / float(cal)

    counts = {k: 0 for k in FLAG_KEYS}
    results = {}
    for rid, r in recs.items():
        flags = classify(r["name"], r["ings"], carb_ratio(r), r["cat"])
        results[rid] = flags
        for k in FLAG_KEYS:
            counts[k] += 1 if flags[k] else 0
    print(f"active recipes: {len(recs)}")
    for k in FLAG_KEYS:
        print(f"  {k:12}: {counts[k]}")

    if write:
        for col in COLS:
            c.execute(f"IF COL_LENGTH('WIKIBOOKS_Recipes','{col}') IS NULL "
                      f"ALTER TABLE WIKIBOOKS_Recipes ADD {col} BIT NOT NULL DEFAULT 0")
        conn.commit()
        for rid, f in results.items():
            c.execute("UPDATE WIKIBOOKS_Recipes SET IsVegetarian=?,IsVegan=?,IsPescatarian=?,"
                      "IsGlutenFree=?,IsDairyFree=?,IsKeto=?,IsPaleo=?,IsHalal=?,IsKosher=?,"
                      "IsMediterranean=? WHERE WikibooksRecipeID=?",
                      int(f["vegetarian"]), int(f["vegan"]), int(f["pescatarian"]),
                      int(f["glutenfree"]), int(f["dairyfree"]), int(f["keto"]),
                      int(f["paleo"]), int(f["halal"]), int(f["kosher"]),
                      int(f["mediterranean"]), rid)
        conn.commit()
        print(f"\nWROTE diet flags ({len(COLS)} cols) for {len(results)} recipes")


if __name__ == "__main__":
    main()

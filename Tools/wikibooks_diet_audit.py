"""Regression audit for the diet flags shipped in wikibooks_bundle.json.gz.
Run this after wikibooks_diet_classify.py + wikibooks_export_bundle.py to confirm the
SHIPPED flags are clean. Uses an INDEPENDENT broad lexicon (different words than the
classifier) but applies the classifier's REMAP/OVERRIDES so plant foods that merely
contain an animal substring (coconut milk, nut milk, chicken egg, creamed corn,
cream of tartar, vegan butter...) are not counted as leaks.

Checks:
  INVARIANTS: vegan -> vegetarian -> pescatarian; vegan -> dairy-free.
  LEAKS: a recipe flagged compatible with a diet must contain NO conflicting indicator.
Exit code 1 on any failure. No DB needed — reads the bundle.
"""
import gzip
import json
import re
import sys
import importlib.util
from pathlib import Path

HERE = Path(__file__).parent
spec = importlib.util.spec_from_file_location("dc", HERE / "wikibooks_diet_classify.py")
dc = importlib.util.module_from_spec(spec); spec.loader.exec_module(dc)
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
recs = json.load(gzip.open(BUNDLE))["recipes"]


def norm(r):
    t = (r["name"] + " || " + " ".join(i.get("description") or "" for i in (r.get("ingredients") or []))).lower()
    for s, d in dc.REMAP:
        t = t.replace(s, d)
    for o in dc.OVERRIDES:
        t = t.replace(o, " ")
    for o in dc.ALCO_OVERRIDES:   # so 'wine vinegar' isn't a false alcohol leak
        t = t.replace(o, " ")
    return t


# Independent lexicon — deliberately different words from the classifier's sets.
ANIMAL = re.compile(r"\b(tripe|sweetbread|liver|gizzard|trotter|shank|cutlet|foie gras|boar|"
                    r"elk|pheasant|partridge|blood sausage|black pudding|haggis|guanciale|"
                    r"lardon|rillette|terrine|chitterling|crackling|squab|offal|oxtail|"
                    r"brisket|prosciutto|pastrami|bresaola|capicola|mortadella|mutton|veal|"
                    r"venison|chicken|beef|pork|lamb|bacon|sausage|turkey|duck|goat meat|ham |"
                    r"iguana|frog|pigeon|quail|buffalo wing|hot wing|chicken wing|filet mignon|"
                    r"prime rib|sirloin|tenderloin|ribeye)", re.I)
NONKOSHER_FISH = re.compile(r"\b(eel|catfish|shark|monkfish|swordfish|sturgeon|dogfish|huss)", re.I)
SEAFOODX = re.compile(r"\b(fish|salmon|tuna|anchovy|sardine|mackerel|herring|trout|halibut|"
                      r"shrimp|prawn|crab|lobster|clam|mussel|oyster|scallop|squid|octopus|caviar|eel)", re.I)
DAIRYX = re.compile(r"\b(creme|cr[eè]me|clotted cream|quark|skyr|labneh|halloumi|feta|brie|"
                    r"gouda|camembert|provolone|gruyere|emmental|gorgonzola|pecorino|havarti|"
                    r"manchego|parmesan|parmigiano|cheddar|mozzarella|asiago|fontina|queso|"
                    r"cotija|colby|milk|cheese|butter|cream|yogurt|ghee|whey|custard)", re.I)
EGGX = re.compile(r"\b(egg|mayonnaise|meringue|albumen)", re.I)
GLUTENX = re.compile(r"\b(udon|ramen|farina|matzo|matzah|matzoh|einkorn|kamut|triticale|"
                     r"freekeh|wheat|flour|bread|pasta|barley|semolina|couscous|rye flour|"
                     r"spelt|seitan|pound cake|sponge cake)", re.I)
PORKX = re.compile(r"\b(pork|bacon|ham |prosciutto|pancetta|guanciale|chorizo|salami|"
                   r"pepperoni|lard|gammon|capicola|mortadella|speck|soppressata)", re.I)
ALCOHOLX = re.compile(r"\b(wine|beer|ale|stout|lager|rum|vodka|whiskey|whisky|brandy|tequila|sake|"
                      r"mirin|sherry|vermouth|bourbon|cognac|liqueur|champagne|marsala|prosecco)", re.I)
SHELLX = re.compile(r"\b(shrimp|prawn|crab|lobster|clam|mussel|oyster|scallop|squid|"
                    r"octopus|crayfish|escargot|snail|cuttlefish|cockle|abalone)", re.I)
GRAINX = re.compile(r"\b(wheat|flour|bread|pasta|rice|oat|corn|barley|rye|quinoa|millet|"
                    r"couscous|bulgur|cornmeal|polenta|grits|cereal|sorghum|buckwheat|noodle)", re.I)
LEGUMEX = re.compile(r"\b(lentil|chickpea|garbanzo|soy|tofu|tempeh|peanut|hummus|edamame|"
                     r"black bean|kidney bean|pinto bean|navy bean|cannellini|lima bean)", re.I)
REDMEATX = re.compile(r"\b(beef|pork|lamb|mutton|veal|venison|bison|goat |bacon|sausage|"
                      r"ham |steak|prosciutto|salami|pepperoni|chorizo|pancetta|guanciale|"
                      r"lard|meatball|meatloaf|hamburger|oxtail|brisket|rabbit)", re.I)

FLAGS = {"isVegan": 0, "isVegetarian": 1, "isPescatarian": 2, "isGlutenFree": 3, "isDairyFree": 4,
         "isKeto": 5, "isPaleo": 6, "isHalal": 7, "isKosher": 8, "isMediterranean": 9}
# diet -> list of patterns that must NOT appear
LEAK_PATTERNS = {
    "isVegan": [ANIMAL, SEAFOODX, DAIRYX, EGGX],
    "isVegetarian": [ANIMAL, SEAFOODX],
    "isPescatarian": [ANIMAL],          # seafood allowed
    "isGlutenFree": [GLUTENX],
    "isDairyFree": [DAIRYX],
    "isHalal": [PORKX, ALCOHOLX],       # no pork, no alcohol
    "isKosher": [PORKX, SHELLX, NONKOSHER_FISH],  # no pork, shellfish, or scaleless fish
    "isPaleo": [GRAINX, LEGUMEX, DAIRYX],  # no grains, legumes, dairy
    "isMediterranean": [REDMEATX],         # no red/processed meat
    # isKeto is macro-based (carb ratio), not keyword — no lexicon leak check.
}

fail = 0

# CATEGORY oracle — independent of the ingredient lexicon (this is what caught the
# tilapia/filet-mignon/hot-dog leaks the keyword audit missed). A recipe's category
# is an authoritative signal: a "Beef recipes"/"Fish recipes" item isn't vegetarian.
CAT_MEAT = re.compile(r"\b(meat|beef|pork|lamb|mutton|veal|venison|bison|goat|chicken|"
                      r"poultry|turkey|duck|game|rabbit|bacon|sausage|ham|offal|steak|hot dog)", re.I)
CAT_SEA = re.compile(r"\b(fish|seafood|salmon|tuna|tilapia|trout|cod|snapper|bass|halibut|"
                     r"shrimp|prawn|crab|lobster|clam|mussel|oyster|scallop|squid|shellfish|"
                     r"sushi|sashimi|ceviche|anchovy|sardine|herring|mackerel|eel)", re.I)
CAT_PORK = re.compile(r"\b(pork|bacon|ham|sausage)", re.I)
CAT_SHELL = re.compile(r"\b(shellfish|shrimp|prawn|crab|lobster|clam|mussel|oyster|scallop|squid)", re.I)
CAT_DAIRY = re.compile(r"\b(cheese|dairy)", re.I)
CAT_REDMEAT = re.compile(r"\b(meat|beef|pork|lamb|mutton|veal|venison|bison|goat|bacon|sausage|ham|steak)", re.I)
CAT_POULTRY = re.compile(r"\b(chicken|poultry|turkey|duck|game hen|fowl)", re.I)
CAT_LEAK = {  # diet -> list of category regexes that must NOT match
    "isVegan": [CAT_MEAT, CAT_SEA, CAT_DAIRY],
    "isVegetarian": [CAT_MEAT, CAT_SEA],
    "isPescatarian": [CAT_MEAT],
    "isHalal": [CAT_PORK],
    "isKosher": [CAT_PORK, CAT_SHELL],
}

print("=== CATEGORY ORACLE (independent of ingredient keywords) ===")
cat_fail = 0
for diet, pats in CAT_LEAK.items():
    hits = []
    for r in recs:
        if not r.get(diet):
            continue
        c = (r.get("category") or "")
        for p in pats:
            if p.search(c):
                hits.append((c, r["name"])); break
    # Mediterranean: red-meat categories, but poultry categories are allowed
    print(f"  {diet:14}: {len(hits)} leaks " + (str(hits[:4]) if hits else ""))
    cat_fail += len(hits)
medhits = [(r.get("category"), r["name"]) for r in recs
           if r.get("isMediterranean") and CAT_REDMEAT.search(r.get("category") or "")
           and not CAT_POULTRY.search(r.get("category") or "")]
print(f"  {'isMediterranean':14}: {len(medhits)} leaks " + (str(medhits[:4]) if medhits else ""))
cat_fail += len(medhits)
# Ambiguous processed meat (sausage/hot-dog family) must not be halal/kosher unless
# qualified as a non-pork meat or vegetarian.
amb = re.compile(r"\b(sausage|frankfurter|bratwurst|kielbasa|bologna|wiener|weiner|hot ?dog|andouille)", re.I)
qual = re.compile(r"\b(beef|chicken|turkey|lamb|veal|duck|goat|vegan|vegetarian|veggie|soy|plant|tofu|fish|halal|kosher)", re.I)
for diet in ("isHalal", "isKosher"):
    sh = [r["name"] for r in recs if r.get(diet) and amb.search(norm(r)) and not qual.search(norm(r))]
    print(f"  {diet+' sausage':22}: {len(sh)} leaks " + (str(sh[:4]) if sh else ""))
    cat_fail += len(sh)
print(f"  category-oracle total: {cat_fail}")
fail += cat_fail

print("\n=== INVARIANTS ===")
inv = {
    "vegan->vegetarian": [r["name"] for r in recs if r.get("isVegan") and not r.get("isVegetarian")],
    "vegetarian->pescatarian": [r["name"] for r in recs if r.get("isVegetarian") and not r.get("isPescatarian")],
    "vegan->dairyfree": [r["name"] for r in recs if r.get("isVegan") and not r.get("isDairyFree")],
}
for k, v in inv.items():
    print(f"  {k:26}: {len(v)} violations {v[:3]}")
    fail += len(v)

print("\n=== LEAKS (shipped flags) ===")
for flag, pats in LEAK_PATTERNS.items():
    hits = []
    for r in recs:
        if not r.get(flag):
            continue
        n = norm(r)
        for p in pats:
            m = p.search(n)
            if m:
                hits.append((r["name"], m.group())); break
    print(f"  {flag:14}: {len(hits)} leaks " + (str(hits[:5]) if hits else ""))
    fail += len(hits)

print(f"\nflag counts: " + str({k: sum(1 for r in recs if r.get(k)) for k in FLAGS}))
print("RESULT:", "PASS — diet flags are clean" if fail == 0 else f"FAIL — {fail} issues")
sys.exit(0 if fail == 0 else 1)

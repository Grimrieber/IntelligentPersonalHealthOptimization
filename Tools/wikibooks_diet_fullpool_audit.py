"""EXHAUSTIVE full-pool diet audit — checks EVERY recipe flagged for each diet,
not a sample. Over-sensitive on purpose: every hit gets human review.
Two independent signals: (1) category bucket, (2) a deliberately broad ingredient lexicon.
Applies the classifier's REMAP+OVERRIDES so plant foods w/ animal substrings aren't false hits.
"""
import gzip, json, re, sys, importlib.util
from collections import Counter
from pathlib import Path

HERE = Path(__file__).parent
dc_spec = importlib.util.spec_from_file_location("dc", HERE / "wikibooks_diet_classify.py")
dc = importlib.util.module_from_spec(dc_spec); dc_spec.loader.exec_module(dc)
b = json.load(gzip.open(HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"))
recs = b["recipes"]

# ---- category buckets (authoritative) ----
MEAT_CATS = {"Meat recipes","Chicken recipes","Pork recipes","Beef recipes","Lamb recipes",
             "Veal recipes","Duck recipes","Turkey recipes","Hamburger recipes","Sausage recipes",
             "Bacon recipes","Ham recipes","Goat recipes","Bison recipes","Mutton recipes",
             "Venison recipes","Poultry recipes","Iguana recipes"}
SEA_CATS  = {"Seafood recipes","Fish recipes","Salmon recipes","Tuna recipes","Shrimp recipes",
             "Crab recipes","Clam recipes","Oyster recipes","Sushi recipes"}
DAIRY_CATS= {"Cheese recipes","Dairy recipes","Butter recipes","Cream recipes","Cheesecake recipes",
             "Macaroni and cheese recipes","Custard recipes","Ice cream recipes","Mousse recipes"}
PORK_CATS = {"Pork recipes","Bacon recipes","Ham recipes","Sausage recipes"}
SHELL_CATS= {"Shrimp recipes","Crab recipes","Clam recipes","Oyster recipes"}  # NOT Seafood (mixed; finned fish is kosher)
NONKOSHER_FISH_X = re.compile(r"\b(eel|catfish|shark|monkfish|swordfish|sturgeon|dogfish|huss)", re.I)

# ---- broad ingredient lexicons (deliberately exhaustive) ----
def P(words): return re.compile(r"\b(" + "|".join(words) + r")", re.I)
MEATX = P(["meat","beef","steak","veal","pork","bacon","gammon","lamb","mutton","hogget",
           "goat meat","venison","elk","moose","caribou","reindeer","bison","boar",
           "rabbit","hare","kangaroo","ostrich","alligator","crocodile","iguana","frog",
           "chicken","rooster","poultry","turkey","duck","duckling","pigeon",
           "squab","pheasant","partridge","quail","guinea fowl","fowl","poussin","game hen",
           "buffalo wing","hot wing","chicken wing","turkey wing",
           "sirloin","tenderloin","ribeye","rib eye","brisket","oxtail","shank","flank",
           "drumstick","gizzard","sweetbread","tripe","trotter","chitterling","haggis",
           "sausage","salami","pepperoni","chorizo","prosciutto","pancetta","guanciale",
           "mortadella","capicola","bresaola","bologna","frankfurter","hot dog","hotdog","wiener",
           "weiner","bratwurst","kielbasa","andouille","jerky","pastrami","spam ","scrapple",
           "foie gras","blood sausage","black pudding","lardon","speck","nduja","soppressata",
           "filet mignon","mignon","chateaubriand","cheesesteak","carnitas","barbacoa","schnitzel",
           "shawarma","souvlaki","kofta","corned beef","mincemeat"])
SEAX = P(["fish","anchovy","sardine","herring","mackerel","salmon","tuna","trout","cod","haddock",
          "halibut","pollock","pollack","plaice","sole","flounder","snapper","bass","perch","pike",
          "carp","catfish","swordfish","monkfish","mahi","grouper","tilapia","sturgeon","caviar",
          "roe ","eel","whitebait","smelt","sprat","kipper","lox","gravlax","bream","turbot","hake",
          "whiting","ling ","barramundi","branzino","pomfret","kingfish","bluefish","marlin",
          "shrimp","prawn","crab","lobster","crayfish","crawfish","langoustine","clam","mussel",
          "oyster","scallop","cockle","whelk","abalone","conch","squid","calamari","octopus",
          "cuttlefish","snail","escargot","urchin","krill","surimi","sushi","sashimi","ceviche",
          "fish sauce","oyster sauce","anchovy paste","shrimp paste","bonito","dashi"])
DAIRYX = P(["milk","cream","half-and-half","butter","ghee","cheese","yogurt","yoghurt","whey",
            "casein","curd","custard","kefir","buttermilk","gelato","dulce de leche","condensed milk",
            "evaporated milk","skyr","labneh","clotted cream","creme fraiche","crème fraîche",
            "sour cream","parmesan","parmigiano","cheddar","mozzarella","feta","brie","gouda",
            "camembert","provolone","pecorino","gorgonzola","gruyere","gruyère","emmental","havarti",
            "manchego","halloumi","colby","cotija","queso","asiago","fontina","raclette","stilton",
            "roquefort","ricotta","mascarpone","paneer","quark"])
EGGX = P(["egg","albumen","mayonnaise","mayo","meringue","aioli"])
HONEYX = P(["honey","beeswax","royal jelly"])
GELX = P(["gelatin","gelatine","rennet","isinglass"])
GLUTENX = P(["wheat","flour","bread","pasta","noodle","cracker","tortilla","breadcrumb","panko",
             "soy sauce","barley","semolina","couscous","bulgur","farro","seitan","orzo","spelt",
             "malt","graham","phyllo","filo","pita","naan","bagel","croissant","pastry","biscuit",
             "muffin","macaroni","vermicelli","durum","matzo","udon","ramen","einkorn","kamut",
             "triticale","freekeh","fettuccine","spaghetti","lasagne","lasagna","ravioli","gnocchi",
             "rye","cake mix","pancake mix"])
PORKX = P(["pork","bacon","ham ","prosciutto","pancetta","guanciale","chorizo","salami","pepperoni",
           "lard","gammon","capicola","mortadella","speck","soppressata"])
ALCX = P(["wine","beer","ale ","lager","rum","vodka","whiskey","whisky","brandy","tequila","sake",
          "mirin","sherry","vermouth","bourbon","cognac","liqueur","champagne","marsala","prosecco",
          "kahlua","schnapps","amaretto"])
SHELLX = P(["shrimp","prawn","crab","lobster","clam","mussel","oyster","scallop","squid","octopus",
            "crayfish","escargot","snail","cuttlefish","cockle","abalone","conch","krill","calamari"])
GRAINX = P(["wheat","flour","bread","pasta","rice","oat","corn","barley","rye","quinoa","millet",
            "couscous","bulgur","cornmeal","polenta","grits","cereal","sorghum","buckwheat","noodle",
            "amaranth","teff","maize"])
LEGUMEX = P(["lentil","chickpea","garbanzo","soy","tofu","tempeh","peanut","hummus","edamame","bean",
             "split pea","miso"])
REDMEATX = P(["beef","pork","lamb","mutton","veal","venison","bison","goat meat","bacon","sausage",
              "ham ","steak","prosciutto","salami","pepperoni","chorizo","pancetta","guanciale","lard",
              "meatball","meatloaf","hamburger","oxtail","brisket","rabbit","filet mignon","prime rib"])

def norm(r):
    t = (r["name"] + " || " + " ".join(i.get("description") or "" for i in (r.get("ingredients") or []))).lower()
    for s, d in dc.REMAP: t = t.replace(s, d)
    for o in dc.OVERRIDES: t = t.replace(o, " ")
    for o in dc.ALCO_OVERRIDES: t = t.replace(o, " ")
    return t

amb_sausage_re = re.compile(r"\b(sausage|frankfurter|bratwurst|kielbasa|bologna|wiener|weiner|hot ?dog|andouille)", re.I)
qual_re = re.compile(r"\b(beef|chicken|turkey|lamb|veal|duck|goat|vegan|vegetarian|veggie|soy|plant|tofu|fish|halal|kosher)", re.I)

def cat(r): return r.get("category") or ""

# diet -> function(r, n) -> trigger string or None
def check(diet, r):
    n = norm(r); c = cat(r)
    if diet == "isVegetarian":
        if c in MEAT_CATS: return f"cat:{c}"
        if c in SEA_CATS:  return f"cat:{c}"
        for rx,lbl in ((MEATX,"meat"),(SEAX,"sea"),(GELX,"gel")):
            m=rx.search(n);
            if m: return f"{lbl}:{m.group().strip()}"
    elif diet == "isVegan":
        if c in MEAT_CATS|SEA_CATS|DAIRY_CATS: return f"cat:{c}"
        for rx,lbl in ((MEATX,"meat"),(SEAX,"sea"),(DAIRYX,"dairy"),(EGGX,"egg"),(HONEYX,"honey"),(GELX,"gel")):
            m=rx.search(n)
            if m: return f"{lbl}:{m.group().strip()}"
    elif diet == "isPescatarian":
        if c in MEAT_CATS: return f"cat:{c}"
        m=MEATX.search(n)
        if m: return f"meat:{m.group().strip()}"
    elif diet == "isGlutenFree":
        m=GLUTENX.search(n)
        if m: return f"gluten:{m.group().strip()}"
    elif diet == "isDairyFree":
        if c in DAIRY_CATS: return f"cat:{c}"
        m=DAIRYX.search(n)
        if m: return f"dairy:{m.group().strip()}"
    elif diet == "isPaleo":
        for rx,lbl in ((GRAINX,"grain"),(LEGUMEX,"legume"),(DAIRYX,"dairy")):
            m=rx.search(n)
            if m: return f"{lbl}:{m.group().strip()}"
    elif diet == "isHalal":
        if c in PORK_CATS: return f"cat:{c}"
        for rx,lbl in ((PORKX,"pork"),(ALCX,"alcohol"),(GELX,"gel")):
            m=rx.search(n)
            if m: return f"{lbl}:{m.group().strip()}"
        if amb_sausage_re.search(n) and not qual_re.search(n): return "amb_sausage"
    elif diet == "isKosher":
        if c in PORK_CATS|SHELL_CATS: return f"cat:{c}"
        for rx,lbl in ((PORKX,"pork"),(SHELLX,"shell"),(NONKOSHER_FISH_X,"scaleless")):
            m=rx.search(n)
            if m: return f"{lbl}:{m.group().strip()}"
        if amb_sausage_re.search(n) and not qual_re.search(n): return "amb_sausage"
        # meat + dairy together
        if (MEATX.search(n) or c in MEAT_CATS) and (DAIRYX.search(n) or c in DAIRY_CATS): return "meat+dairy"
    elif diet == "isMediterranean":
        if c in MEAT_CATS and c not in {"Chicken recipes","Turkey recipes","Duck recipes","Poultry recipes"}:
            return f"cat:{c}"
        m=REDMEATX.search(n)
        if m: return f"redmeat:{m.group().strip()}"
    return None

DIETS = ["isVegetarian","isVegan","isPescatarian","isGlutenFree","isDairyFree","isPaleo",
         "isHalal","isKosher","isMediterranean"]
total=0
for diet in DIETS:
    pool=[r for r in recs if r.get(diet)]
    leaks=[(check(diet,r), r["name"], cat(r)) for r in pool]
    leaks=[x for x in leaks if x[0]]
    trig=Counter(x[0].split(":")[0] for x in leaks)
    print(f"\n{diet}: pool={len(pool)}  LEAKS={len(leaks)}  triggers={dict(trig)}")
    for t,nm,c in leaks[:25]:
        print(f"    [{t}] {nm}  <{c}>")
    total+=len(leaks)
print(f"\n==== TOTAL LEAKS ACROSS ALL DIETS: {total} ====")
print("RESULT:", "PASS" if total == 0 else f"FAIL — {total} leaks")
sys.exit(0 if total == 0 else 1)

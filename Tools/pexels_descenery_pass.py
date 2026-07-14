"""
Targeted re-search for recipes whose NAME contains a place/nationality word
(e.g. "Austrian Meatloaf", "Buffalo Wings", "Brazilian Feijoada"). Pexels tends
to return the PLACE or the ANIMAL for those queries — we saw a mountain lake for
"Austrian Meatloaf", a street scene for "Feijoada", and a literal buffalo for
"Buffalo Wings". This pass re-queries each with a FOOD-BIASED query and a strict
alt-text filter that rejects scenery / animals / people / places, so only actual
food photos survive.

Writes ONLY to descenery_cache.json (name -> better url). It does NOT touch the
bundle, so it is safe to run alongside the main pexels_recipe_images.py pass.
Apply step (apply_image_caches.py) merges it into the bundle afterwards, with the
descenery result winning over the original Pexels match for these names.

Reads PEXELS_API_KEY from env (never hard-coded/committed).
"""
import gzip, json, os, re, time, urllib.parse, urllib.request
from pathlib import Path
from urllib.parse import urlparse

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
CACHE = HERE / "descenery_cache.json"
API = "https://api.pexels.com/v1/search"
KEY = os.environ.get("PEXELS_API_KEY", "").strip()
PACE = 18.5

PLACE = ["austrian","bavarian","german","french","italian","spanish","greek","turkish",
    "russian","polish","hungarian","swedish","norwegian","danish","dutch","belgian","swiss",
    "portuguese","english","irish","scottish","welsh","british","american","mexican","brazilian",
    "argentine","peruvian","cuban","jamaican","moroccan","egyptian","ethiopian","nigerian","kenyan",
    "indian","pakistani","thai","vietnamese","chinese","japanese","korean","filipino","indonesian",
    "malaysian","lebanese","syrian","persian","iranian","iraqi","afghan","australian","canadian",
    "hawaiian","cajun","creole","sicilian","tuscan","provencal","catalan","basque","georgian",
    "armenian","ukrainian","czech","slovak","croatian","serbian","romanian","bulgarian","finnish",
    "icelandic","tibetan","nepalese","bangladeshi","sri lankan","cambodian","burmese","mongolian",
    "grand union","new england","southern","texas","memphis","kansas city","buffalo","philadelphia",
    "boston","chicago","california","florida"]

# alt-text words that mean the photo is NOT a plate of food
REJECT = [
    # scenery / places
    "landscape","mountain","mountains","lake","river","valley","village","town","city","skyline",
    "street","road","building","buildings","cathedral","church","castle","bridge","harbor","harbour",
    "canal","monument","square","panorama","aerial","hill","hills","field","farm","forest","beach",
    "island","coast","sky","cloud","sunset","sunrise","nature","outdoor scenery","tower","museum",
    "station","market street","storefront","shop","architecture","snow","winter landscape",
    # animals (Buffalo -> buffalo, etc.)
    "buffalo","bison","cow","cattle","bull","animal","wildlife","bird","birds","horse","sheep",
    "goat","deer","elephant","lion","tiger","zoo","pet","dog","cat ",
    # people
    "person","people"," man ","woman","women"," men ","chef","hand","holding","eating","kid",
    "child"," boy"," girl","waiter","female","male","selfie","portrait","hands","crowd","tourist",
    # abstract / non-food
    "flag","map","logo","sign","poster","car","vehicle","boat","ship","train","plane",
]
# words that positively indicate food (prefer these)
FOOD = ["food","dish","meal","plate","bowl","soup","stew","salad","cake","bread","pie","pasta",
    "rice","meat","chicken","beef","pork","fish","seafood","dessert","cookie","cooked","served",
    "cuisine","sauce","curry","noodle","dumpling","grilled","baked","fried","roast","breakfast",
    "lunch","dinner","snack","pastry","tart","pudding","casserole"]

def clean(name):
    n = re.sub(r"\(.*?\)", "", name)
    n = n.replace("'", "").replace('"', "").replace("&", "and")
    n = re.sub(r"^[\dIVXivx\-\.\s]+", "", n)
    n = re.sub(r"\s+[IVX]+$", "", n)
    return re.sub(r"\s+", " ", n).strip()

def strip_place(name):
    """Drop leading place words so 'Buffalo Wings' -> 'Wings', 'Austrian Meatloaf' -> 'Meatloaf'."""
    words = name.split()
    while words and words[0].lower().rstrip(",") in PLACE:
        words.pop(0)
    return " ".join(words).strip() or name

def search(query):
    if not query:
        return None
    params = {"query": query, "per_page": "15", "orientation": "square"}
    for attempt in range(6):
        req = urllib.request.Request(API + "?" + urllib.parse.urlencode(params),
                                     headers={"Authorization": KEY,
                                              "User-Agent": "Mozilla/5.0 (compatible; IPHO-RecipeImages/1.0)"})
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                js = json.load(r)
            break
        except urllib.error.HTTPError as e:
            if e.code == 429:
                wait = int(e.headers.get("Retry-After", 0)) or 60
                print(f"    429 - backoff {wait}s"); time.sleep(wait); continue
            if e.code in (401, 403):
                raise SystemExit("Pexels API key rejected - check PEXELS_API_KEY")
            return None
        except Exception:
            time.sleep(5); continue
    else:
        return None

    best = None
    for p in js.get("photos", []):
        alt = (p.get("alt") or "").lower()
        if any(b in alt for b in REJECT):
            continue
        src = p.get("src", {})
        url = src.get("large") or src.get("medium") or src.get("original")
        if not url:
            continue
        if any(f in alt for f in FOOD):
            return url            # strong food match - take immediately
        if best is None:
            best = url             # non-rejected fallback (empty alt etc.)
    return best

def main():
    if not KEY:
        raise SystemExit("Set PEXELS_API_KEY env var first.")
    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    targets = [r["name"] for r in b["recipes"]
               if r.get("imageUrl") and urlparse(r["imageUrl"]).netloc == "images.pexels.com"
               and any(pw in r["name"].lower() for pw in PLACE)]
    print(f"{len(targets)} place-named recipes to re-search (~{len(targets)*PACE/3600:.1f} h)")

    cache = json.loads(CACHE.read_text(encoding="utf-8")) if CACHE.exists() else {}
    print(f"resume: {len(cache)} done")

    new = 0
    for i, name in enumerate(targets):
        if name in cache:
            continue
        food_q = strip_place(clean(name))
        # food-biased: "<dish> food dish" biases Pexels toward a plate, not a place/animal
        url = search(f"{food_q} food dish") or search(f"{food_q} recipe") or search(food_q)
        cache[name] = url or ""
        new += 1
        if new % 15 == 0:
            CACHE.write_text(json.dumps(cache), encoding="utf-8")
            got = sum(1 for v in cache.values() if v)
            print(f"  {new} searched; {got} matched; {i+1}/{len(targets)}")
        time.sleep(PACE)

    CACHE.write_text(json.dumps(cache), encoding="utf-8")
    got = sum(1 for v in cache.values() if v)
    print(f"done: {got}/{len(targets)} got a food-biased photo")

if __name__ == "__main__":
    main()

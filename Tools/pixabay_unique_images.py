"""
De-duplicate recipe photos using PIXABAY (100 req/min vs Pexels' 200/hr → the
whole ~1600-recipe dedup runs in ~20 min instead of ~8 h). Same idea as
pexels_unique_images.py: pull a deep pool per query and global-greedy-assign so
each recipe gets a photo not already used.

Two Pixabay-specific wins:
  * category=food biases hard to actual food.
  * `tags` (comma keywords) works like alt text for rejecting scenery/animals/people.
Query building prefers the ENGLISH description in parentheses for foreign-named
dishes ("Chibwabwa (Zambian Pumpkin Leaves…)" → "pumpkin leaves" — Pixabay has 0
hits for "Chibwabwa" but 500 for the description).

Mode (argv[1]): dupes (recipes whose CURRENT image is shared) | all.
Pools cached in pixabay_pools.json. Reads PIXABAY_API_KEY from env (never committed).
Pace argv[2] seconds (default 0.7 → under 100/min). Writes assignments into the bundle.
"""
import gzip, json, os, re, sys, time, urllib.parse, urllib.request
from pathlib import Path
from collections import Counter

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
POOLS = HERE / "pixabay_pools.json"
API = "https://pixabay.com/api/"
KEY = os.environ.get("PIXABAY_API_KEY", "").strip()

PLACE = ["austrian","bavarian","german","french","italian","spanish","greek","turkish","russian",
    "polish","hungarian","swedish","norwegian","danish","dutch","belgian","swiss","portuguese",
    "english","irish","scottish","welsh","british","american","mexican","brazilian","argentine",
    "peruvian","cuban","jamaican","moroccan","egyptian","ethiopian","nigerian","kenyan","indian",
    "pakistani","thai","vietnamese","chinese","japanese","korean","filipino","indonesian","malaysian",
    "lebanese","syrian","persian","iranian","iraqi","afghan","australian","canadian","hawaiian","cajun",
    "creole","sicilian","tuscan","provencal","catalan","basque","georgian","armenian","ukrainian",
    "czech","slovak","croatian","serbian","romanian","bulgarian","finnish","icelandic","tibetan",
    "nepalese","bangladeshi","cambodian","burmese","mongolian","zambian","rwandan","ugandan","gambian",
    "ghanaian","southern","texas","memphis","buffalo","boston","chicago","california","florida"]

# tag words that mean the photo is NOT a plate of food
REJECT = ["landscape","mountain","lake","river","valley","village","town","city","skyline","street",
    "building","cathedral","church","castle","bridge","canal","monument","panorama","aerial","forest",
    "beach","island","coast","sky","sunset","nature","architecture","animal","wildlife","bird","cow",
    "cattle","bull","buffalo","bison","horse","sheep","goat","deer","dog","cat","pet","zoo","person",
    "people","man","woman","women","men","child","kid","boy","girl","portrait","selfie","crowd",
    "flag","map","logo","car","vehicle","boat","ship"]

def clean(name):
    n = re.sub(r"[\"'&]", " ", name)
    n = re.sub(r"^[\dIVXivx\-\.\s]+", "", n)
    n = re.sub(r"\s+[IVX]+$", "", n)
    return re.sub(r"\s+", " ", n).strip()

def strip_place(s):
    w = s.split()
    while w and w[0].lower().rstrip(",") in PLACE:
        w.pop(0)
    return " ".join(w).strip() or s

def query_for(name):
    """Prefer the English description in parens for foreign-named dishes; else the name."""
    paren = re.search(r"\((.*?)\)", name)
    main = re.sub(r"\(.*?\)", "", name).strip()
    desc = paren.group(1).strip() if paren else ""
    # 'with', 'and' clutter — keep the head noun phrase
    base = desc if (desc and len(desc.split()) >= 2) else main
    base = re.sub(r"\bwith\b.*$", "", base, flags=re.I).strip() or base
    return strip_place(clean(base)).lower()

def fetch_pool(query, pace):
    def call(params):
        req = urllib.request.Request(API + "?" + urllib.parse.urlencode(params),
                                     headers={"User-Agent": "Mozilla/5.0"})
        for _ in range(5):
            try:
                with urllib.request.urlopen(req, timeout=30) as r:
                    return json.load(r)
            except urllib.error.HTTPError as e:
                if e.code == 429:
                    time.sleep(int(e.headers.get("Retry-After", 0)) or 30); continue
                if e.code in (400, 401): raise SystemExit(f"Pixabay key rejected ({e.code})")
                return None
            except Exception:
                time.sleep(3); continue
        return None

    base = {"key": KEY, "image_type": "photo", "per_page": "150", "safesearch": "true"}
    js = call(dict(base, q=query[:100], category="food"))
    if not js or js.get("totalHits", 0) < 5:            # broaden if food category is thin
        js = call(dict(base, q=query[:100])) or js
    time.sleep(pace)
    if not js:
        return []
    food, other = [], []
    for h in js.get("hits", []):
        tags = (h.get("tags") or "").lower()
        if any(b in tags.split(", ") or b in tags for b in REJECT):
            continue
        url = h.get("webformatURL")
        if not url:
            continue
        (food if "food" in tags or "dish" in tags or "meal" in tags else other).append(url)
    return food + other

def main():
    if not KEY:
        raise SystemExit("Set PIXABAY_API_KEY env var first.")
    mode = sys.argv[1] if len(sys.argv) > 1 else "dupes"
    pace = float(sys.argv[2]) if len(sys.argv) > 2 else 0.7

    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    for r in recipes:
        r["_q"] = query_for(r["name"])

    if mode == "dupes":
        ic = Counter(r.get("imageUrl") for r in recipes if r.get("imageUrl"))
        in_scope = [r for r in recipes if r.get("imageUrl") and ic[r["imageUrl"]] > 1]
    else:
        in_scope = recipes
    scope = {r["name"] for r in in_scope}
    queries = sorted({r["_q"] for r in in_scope})
    print(f"mode={mode}: {len(in_scope)} recipes, {len(queries)} queries (~{len(queries)*pace/60:.0f} min)")

    pools = json.loads(POOLS.read_text(encoding="utf-8")) if POOLS.exists() else {}
    for i, q in enumerate(queries):
        if q not in pools:
            pools[q] = fetch_pool(q, pace)
            if (i + 1) % 25 == 0:
                POOLS.write_text(json.dumps(pools), encoding="utf-8")
                print(f"  pools {i+1}/{len(queries)}", flush=True)
    POOLS.write_text(json.dumps(pools), encoding="utf-8")

    used = {r["imageUrl"] for r in recipes if r["name"] not in scope and r.get("imageUrl")}
    assigned = kept = 0
    for r in in_scope:
        pick = next((u for u in (pools.get(r["_q"]) or []) if u not in used), None)
        if pick:
            r["imageUrl"] = pick; used.add(pick); assigned += 1
        elif r.get("imageUrl"):
            used.add(r["imageUrl"]); kept += 1

    for r in recipes:
        r.pop("_q", None)
    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)

    ids = Counter(r["imageUrl"] for r in recipes if r.get("imageUrl"))
    shared = sum(v for v in ids.values() if v > 1)
    print(f"assigned {assigned}, kept {kept}. {len(ids)} distinct images; "
          f"{shared} recipes still share ({100*shared/len(recipes):.1f}%). schemaVersion={b['schemaVersion']}")

if __name__ == "__main__":
    main()

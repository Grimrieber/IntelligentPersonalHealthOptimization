"""
Guarantee EVERY recipe has an image. Recipes with their own Wikibooks page photo
(added by wikibooks_add_images.py) keep it. For the rest, pull category-relevant
food photos from Wikimedia Commons (CC-licensed) — several per category, then
round-robin them across that category's recipes so they don't all look identical.

DB-free: reads/writes Resources/Raw/wikibooks_bundle.json.gz. Run AFTER
wikibooks_add_images.py.
"""
import gzip, json, time, urllib.parse, urllib.request
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
API = "https://commons.wikimedia.org/w/api.php"
UA = "IPHO-RecipeImages/1.0 (personal health app; category food photos from Wikimedia Commons)"
PER_CATEGORY = 12

BAD = ["logo", "commons-", "wikipedia", "wikivoyage", "wiktionary", "icon", "symbol",
       "coat_of_arms", ".svg", "mask", "einweg", "respirator", "filter", "template",
       "datebar", "diagram", "_map.", "coatofarms"]

def ok_photo(title: str) -> bool:
    tl = title.lower()
    if not (tl.endswith(".jpg") or tl.endswith(".jpeg") or tl.endswith(".png")):
        return False
    return not any(b in tl for b in BAD)

def api_get(params):
    params = dict(params, format="json", maxlag="5")
    for attempt in range(6):
        req = urllib.request.Request(API + "?" + urllib.parse.urlencode(params), headers={"User-Agent": UA})
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                return json.load(r)
        except urllib.error.HTTPError as e:
            if e.code == 429:
                wait = int(e.headers.get("Retry-After", 0)) or (5 * (attempt + 1))
                print(f"    429 — backoff {wait}s"); time.sleep(wait); continue
            raise
    return {}

def query_for(category: str) -> str:
    # Use the category's own noun (e.g. "Dessert", "Chocolate cake", "Chicken") —
    # NOT a generic "food dish" suffix, which pulls in off-topic/savory results
    # (a beef photo in the dessert pool). Plain, specific queries keep pools on-topic.
    c = (category or "").replace(" recipes", "").replace(" recipe", "").strip()
    if not c or c.lower() == "uncategorized":
        return "homemade meal plated"
    return c

def search_files(q):
    js = api_get({"action": "query", "list": "search", "srnamespace": "6",
                  "srsearch": q, "srlimit": "24"})
    hits = [h["title"] for h in js.get("query", {}).get("search", [])]
    return [t for t in hits if ok_photo(t)][:PER_CATEGORY]

def thumbs(file_titles):
    out = {}
    files = list(file_titles)
    for i in range(0, len(files), 50):
        js = api_get({"action": "query", "prop": "imageinfo", "iiprop": "url",
                      "iiurlwidth": "500", "titles": "|".join(files[i:i+50])})
        for _, p in js.get("query", {}).get("pages", {}).items():
            ii = p.get("imageinfo", [])
            if ii:
                out[p.get("title", "")] = ii[0].get("thumburl") or ii[0].get("url")
        time.sleep(1.1)
    return out

def main():
    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    imageless = [r for r in recipes if not r.get("imageUrl")]
    print(f"{len(recipes)} recipes, {len(imageless)} without an image")

    by_cat = defaultdict(list)
    for r in imageless:
        by_cat[r.get("category") or "Uncategorized"].append(r)
    print(f"{len(by_cat)} categories to fill")

    all_files = {}          # category -> [File:...]
    for i, cat in enumerate(sorted(by_cat), 1):
        files = search_files(query_for(cat))
        if not files:        # fallback to a generic food query
            files = search_files("food dish meal")
        all_files[cat] = files
        if i % 25 == 0:
            print(f"  searched {i}/{len(by_cat)} categories")
        time.sleep(1.1)

    # Resolve every unique File: to a thumbnail URL in one batched pass.
    unique = {f for files in all_files.values() for f in files}
    print(f"resolving {len(unique)} unique image thumbnails…")
    thumb = thumbs(unique)

    filled = 0
    for cat, rs in by_cat.items():
        urls = [thumb[f] for f in all_files.get(cat, []) if f in thumb]
        if not urls:
            continue
        for i, r in enumerate(rs):
            r["imageUrl"] = urls[i % len(urls)]   # round-robin for variety
            filled += 1

    still = sum(1 for r in recipes if not r.get("imageUrl"))
    print(f"filled {filled} recipes; {still} still without an image")

    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)
    print(f"wrote {BUNDLE} (schemaVersion={b['schemaVersion']}); "
          f"total with image: {sum(1 for r in recipes if r.get('imageUrl'))}/{len(recipes)}")

if __name__ == "__main__":
    main()

"""
Accurate per-recipe images. For every recipe WITHOUT its own Wikibooks page photo,
search Wikimedia Commons by the recipe's NAME (e.g. "Barbecue Meatloaf") and take
the top food photo — far more accurate than a shared category image. Names that
match nothing fall back to a category-relevant photo, so coverage stays 100%.

One Commons request per recipe (generator=search + imageinfo in a single call).
Resumable: caches name->url in perrecipe_images_cache.json so a restart continues.

Run AFTER wikibooks_add_images.py (which supplies the ~20% exact page photos).
"""
import gzip, json, re, time, urllib.parse, urllib.request
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
CACHE = HERE / "perrecipe_images_cache.json"
API = "https://commons.wikimedia.org/w/api.php"
UA = "IPHO-RecipeImages/1.0 (personal health app; per-recipe food photos from Wikimedia Commons)"

BAD = ["logo", "commons-", "wikipedia", "wikivoyage", "wiktionary", "icon", "symbol",
       "coat_of_arms", ".svg", "mask", "einweg", "respirator", "filter", "template",
       "datebar", "diagram", "_map.", "flag_", "poster", "sign_"]

def ok_photo(title: str) -> bool:
    tl = title.lower()
    if not (tl.endswith(".jpg") or tl.endswith(".jpeg") or tl.endswith(".png")):
        return False
    return not any(b in tl for b in BAD)

def clean_name(name: str) -> str:
    n = re.sub(r"\(.*?\)", "", name)                 # drop parentheticals
    n = n.replace("'", "").replace('"', "").replace("&", "and")
    n = re.sub(r"^[\dIVXivx\-\.\s]+", "", n)         # drop leading numbers/roman/dashes
    n = re.sub(r"\s+[IVX]+$", "", n)                 # drop trailing roman numerals (II, III)
    return re.sub(r"\s+", " ", n).strip()

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
                time.sleep(wait); continue
            raise
        except Exception:
            time.sleep(3); continue
    return {}

def find_by_name(query):
    if not query:
        return None
    js = api_get({"action": "query", "generator": "search", "gsrnamespace": "6",
                  "gsrsearch": query, "gsrlimit": "8", "prop": "imageinfo",
                  "iiprop": "url", "iiurlwidth": "500"})
    pages = js.get("query", {}).get("pages", {})
    for p in sorted(pages.values(), key=lambda x: x.get("index", 999)):
        if ok_photo(p.get("title", "")):
            ii = p.get("imageinfo", [])
            if ii:
                return ii[0].get("thumburl") or ii[0].get("url")
    return None

def category_pool(categories):
    """One search per category -> up to 10 food photos each (fallback)."""
    pool = {}
    for i, cat in enumerate(sorted(categories), 1):
        c = (cat or "").replace(" recipes", "").replace(" recipe", "").strip() or "homemade meal plated"
        js = api_get({"action": "query", "generator": "search", "gsrnamespace": "6",
                      "gsrsearch": c, "gsrlimit": "10", "prop": "imageinfo",
                      "iiprop": "url", "iiurlwidth": "500"})
        urls = []
        for p in sorted(js.get("query", {}).get("pages", {}).values(), key=lambda x: x.get("index", 999)):
            if ok_photo(p.get("title", "")):
                ii = p.get("imageinfo", [])
                if ii:
                    urls.append(ii[0].get("thumburl") or ii[0].get("url"))
        pool[cat] = urls
        if i % 25 == 0:
            print(f"  category pools {i}/{len(categories)}")
        time.sleep(1.1)
    return pool

def main():
    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    imageless = [r for r in recipes if not r.get("imageUrl")]
    print(f"{len(recipes)} recipes, {len(imageless)} need a per-recipe photo")

    cache = json.loads(CACHE.read_text(encoding="utf-8")) if CACHE.exists() else {}
    print(f"resume cache: {len(cache)} names already resolved")

    # Category pools (fallback for names that match nothing)
    cats = {r.get("category") or "Uncategorized" for r in imageless}
    print(f"building {len(cats)} category fallback pools…")
    pools = category_pool(cats)
    pool_idx = defaultdict(int)

    done = 0
    for r in imageless:
        name = r["name"]
        if name in cache:
            url = cache[name]
        else:
            url = find_by_name(clean_name(name)) or find_by_name(name)
            cache[name] = url or ""
            done += 1
            if done % 100 == 0:
                CACHE.write_text(json.dumps(cache), encoding="utf-8")
                got = sum(1 for v in cache.values() if v)
                print(f"  searched {done} new; {got} matched by name so far")
            time.sleep(1.1)

        if not url:  # name matched nothing -> category pool
            cat = r.get("category") or "Uncategorized"
            urls = pools.get(cat) or []
            if urls:
                url = urls[pool_idx[cat] % len(urls)]
                pool_idx[cat] += 1
        if url:
            r["imageUrl"] = url

    CACHE.write_text(json.dumps(cache), encoding="utf-8")
    named = sum(1 for v in cache.values() if v)
    total = sum(1 for r in recipes if r.get("imageUrl"))
    print(f"name-matched: {named}/{len(imageless)} | total with image: {total}/{len(recipes)}")

    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)
    print(f"wrote {BUNDLE} (schemaVersion={b['schemaVersion']})")

if __name__ == "__main__":
    main()

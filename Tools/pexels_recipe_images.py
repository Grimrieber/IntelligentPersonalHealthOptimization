"""
Fetch real dish photos from Pexels (curated food library) for every recipe that
has no verified Wikibooks page photo. Pexels food searches return actual food
(not canals/appliances), and we skip any result whose description mentions people
so there are no humans. Recipes with their own page photo keep it.

Reads the API key from the PEXELS_API_KEY env var (never hard-coded/committed).
Resumable: caches name->url in pexels_cache.json. Rate-limited to Pexels' 200/hr
free tier, so it paces ~18s/request and runs for several hours in the background.

Stores the Pexels CDN url (hotlink-friendly per Pexels license; attribution not
required). DB-free: reads/writes Resources/Raw/wikibooks_bundle.json.gz.
"""
import gzip, json, os, re, time, urllib.parse, urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
CACHE = HERE / "pexels_cache.json"
API = "https://api.pexels.com/v1/search"
KEY = os.environ.get("PEXELS_API_KEY", "").strip()
PACE = 18.5   # seconds between requests → stays under 200/hr

HUMAN = ["person", "people", " man", "woman", "women", " men", "chef", "hand",
         "holding", "eating", "kid", "child", " boy", " girl", "cook ", "waiter",
         "female", "male", "selfie", "portrait", "hands"]

def clean(name: str) -> str:
    n = re.sub(r"\(.*?\)", "", name)
    n = n.replace("'", "").replace('"', "").replace("&", "and")
    n = re.sub(r"^[\dIVXivx\-\.\s]+", "", n)
    n = re.sub(r"\s+[IVX]+$", "", n)
    return re.sub(r"\s+", " ", n).strip()

def search(query: str):
    if not query:
        return None
    params = {"query": query, "per_page": "10", "orientation": "square"}
    for attempt in range(6):
        # Browser-like UA required — Pexels' Cloudflare returns 403 (error 1010) otherwise.
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
                print(f"    429 — backoff {wait}s"); time.sleep(wait); continue
            if e.code in (401, 403):
                raise SystemExit("Pexels API key rejected — check PEXELS_API_KEY")
            return None
        except Exception:
            time.sleep(5); continue
    else:
        return None

    for p in js.get("photos", []):
        alt = (p.get("alt") or "").lower()
        if any(h in alt for h in HUMAN):
            continue                     # no humans
        src = p.get("src", {})
        url = src.get("large") or src.get("medium") or src.get("original")
        if url:
            return url
    return None

def main():
    if not KEY:
        raise SystemExit("Set PEXELS_API_KEY env var first.")

    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    imageless = [r for r in recipes if not r.get("imageUrl")]
    print(f"{len(recipes)} recipes, {len(imageless)} need a Pexels photo "
          f"(~{len(imageless)*PACE/3600:.1f} h at 200/hr)")

    cache = json.loads(CACHE.read_text(encoding="utf-8")) if CACHE.exists() else {}
    print(f"resume cache: {len(cache)} names done")

    new = 0
    for i, r in enumerate(imageless):
        name = r["name"]
        if name not in cache:
            url = search(clean(name)) or search(name)
            cache[name] = url or ""
            new += 1
            if new % 20 == 0:
                CACHE.write_text(json.dumps(cache), encoding="utf-8")
                got = sum(1 for v in cache.values() if v)
                print(f"  {new} new searched; {got} matched; {i+1}/{len(imageless)}")
            time.sleep(PACE)
        if cache.get(name):
            r["imageUrl"] = cache[name]

    CACHE.write_text(json.dumps(cache), encoding="utf-8")
    total = sum(1 for r in recipes if r.get("imageUrl"))
    print(f"matched by Pexels: {sum(1 for v in cache.values() if v)}/{len(imageless)} | "
          f"total with image: {total}/{len(recipes)}")

    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)
    print(f"wrote {BUNDLE} (schemaVersion={b['schemaVersion']})")

if __name__ == "__main__":
    main()

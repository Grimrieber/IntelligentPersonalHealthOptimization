"""
De-duplicate recipe photos. The earlier passes always took Pexels' #1 result, so
popular stock photos landed on many unrelated recipes (65% of recipes shared an
image; one photo was on 36 recipes). This pass pulls a DEEP pool (per_page=80)
per distinct query and does global greedy assignment so each recipe gets a
photo not already used elsewhere.

Modes (argv[1]):
  shared  - only recipes whose query is shared by >1 recipe (fixes same-name dupes
            like the 6 "meatloaf" recipes). Fast: ~one search per shared query.
  all     - every recipe (also fixes cross-query collisions). Slow: ~2674 searches.

Pools are cached in unique_pools.json (query -> [urls]) so re-runs are cheap and
assignment is deterministic. Assignments written straight into the bundle.
Reads PEXELS_API_KEY from env. Pace via argv[2] seconds (default 18.5).
"""
import gzip, json, os, sys, time, urllib.parse, urllib.request, importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
POOLS = HERE / "unique_pools.json"
API = "https://api.pexels.com/v1/search"
KEY = os.environ.get("PEXELS_API_KEY", "").strip()

# reuse the good filters + query cleaners from the descenery pass
_spec = importlib.util.spec_from_file_location("desc", HERE / "pexels_descenery_pass.py")
_desc = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(_desc)
REJECT, FOOD, clean, strip_place = _desc.REJECT, _desc.FOOD, _desc.clean, _desc.strip_place

def query_for(name):
    return strip_place(clean(name)).lower() or name.lower()

def fetch_pool(query, pace):
    """Return an ORDERED list of candidate photo urls (food first, scenery/animal/human dropped)."""
    params = {"query": f"{query} food dish", "per_page": "80", "orientation": "square"}
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
                raise SystemExit("Pexels key rejected")
            return []
        except Exception:
            time.sleep(5); continue
    else:
        return []
    food, other = [], []
    for p in js.get("photos", []):
        alt = (p.get("alt") or "").lower()
        if any(b in alt for b in REJECT):
            continue
        src = p.get("src", {})
        url = src.get("large") or src.get("medium") or src.get("original")
        if not url:
            continue
        (food if any(f in alt for f in FOOD) else other).append(url)
    time.sleep(pace)
    return food + other      # food-relevant first, then neutral

def main():
    if not KEY:
        raise SystemExit("Set PEXELS_API_KEY env var first.")
    mode = sys.argv[1] if len(sys.argv) > 1 else "shared"
    pace = float(sys.argv[2]) if len(sys.argv) > 2 else 18.5

    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    # query per recipe, in stable order
    for r in recipes:
        r["_q"] = query_for(r["name"])
    from collections import Counter
    qcount = Counter(r["_q"] for r in recipes)

    if mode == "shared":
        in_scope = [r for r in recipes if qcount[r["_q"]] > 1]
    elif mode == "dupes":
        # only recipes whose CURRENT image is shared by >1 recipe (the real duplicates)
        icount = Counter(r.get("imageUrl") for r in recipes if r.get("imageUrl"))
        in_scope = [r for r in recipes if r.get("imageUrl") and icount[r["imageUrl"]] > 1]
    else:
        in_scope = recipes
    scope_names = {r["name"] for r in in_scope}
    queries = sorted({r["_q"] for r in in_scope})
    print(f"mode={mode}: {len(in_scope)} recipes, {len(queries)} distinct queries "
          f"(~{len(queries)*pace/60:.0f} min)")

    pools = json.loads(POOLS.read_text(encoding="utf-8")) if POOLS.exists() else {}
    for i, q in enumerate(queries):
        if q not in pools:
            pools[q] = fetch_pool(q, pace)
            if (i + 1) % 10 == 0:
                POOLS.write_text(json.dumps(pools), encoding="utf-8")
                print(f"  pools {i+1}/{len(queries)}")
    POOLS.write_text(json.dumps(pools), encoding="utf-8")

    # seed used-set with images of recipes we are NOT touching, so we don't collide with them
    used = {r["imageUrl"] for r in recipes if r["name"] not in scope_names and r.get("imageUrl")}
    assigned = kept = 0
    for r in in_scope:                       # stable order = deterministic
        pool = pools.get(r["_q"]) or []
        pick = next((u for u in pool if u not in used), None)
        if pick:
            r["imageUrl"] = pick; used.add(pick); assigned += 1
        else:
            # pool empty or fully consumed — keep whatever it had (may still dupe)
            if r.get("imageUrl"):
                used.add(r["imageUrl"]); kept += 1

    for r in recipes:
        r.pop("_q", None)
    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)

    # report resulting duplication
    from collections import Counter as C
    ids = C(r["imageUrl"] for r in recipes if r.get("imageUrl"))
    shared = sum(v for v in ids.values() if v > 1)
    print(f"assigned {assigned}, kept {kept}. now {len(ids)} distinct images; "
          f"{shared} recipes still share one ({100*shared/len(recipes):.1f}%). schemaVersion={b['schemaVersion']}")

if __name__ == "__main__":
    main()

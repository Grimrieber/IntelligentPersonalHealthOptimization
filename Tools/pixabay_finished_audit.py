"""
FULL IMAGE AUDIT: make every recipe photo a FINISHED, PLATED dish (so users see
what they're cooking) — not raw ingredients, prep shots, or appliances.

The earlier passes filtered humans/scenery but still let RAW/PREP photos through
(e.g. "Cognac Beef Stew" got raw beef + onions, and three beef stews got
near-identical raw-meat stock shots). Pixabay's `tags` field lets us score each
candidate:
  * REJECT outright: scenery / animals / people.
  * RAW/PREP (raw, uncooked, ingredient, butcher, market…): a candidate that is
    RAW and NOT also cooked/plated is skipped.
  * PREFER: plated / served / cooked / dish / bowl / gourmet / roasted / baked…
  * RELEVANCE: +1 per query word present in the tags, so a "beef stew" query
    prefers an actual stew over a generic (but finished) meat pie.
Best-scoring UNUSED candidate wins → finished dishes, on-topic, 0 duplicates.

Matching is WORD-LEVEL (tag split into words) so "car" (vehicle) no longer
rejects "carrots"; multi-word keywords ("raw meat") match as a substring.

Scope: every recipe whose current image is NOT a Wikimedia exact-recipe page photo
(those are already the finished dish). Pools store url+tags in finished_pools.json
so the scorer can be re-tuned without re-fetching. Reads PIXABAY_API_KEY from env.
argv[1]=pace secs (default 0.8), argv[2]=rescore (skip fetch, just re-score pools).
"""
import gzip, json, os, sys, time, urllib.parse, urllib.request
from pathlib import Path
from collections import Counter
from urllib.parse import urlparse

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
POOLS = HERE / "finished_pools.json"
API = "https://pixabay.com/api/"
KEY = os.environ.get("PIXABAY_API_KEY", "").strip()

import importlib.util
_spec = importlib.util.spec_from_file_location("px", HERE / "pixabay_unique_images.py")
_px = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(_px)
query_for = _px.query_for
# extend the shared reject list: no body parts (the user's "no humans" rule) and a few
# process/non-dish tags that slipped through (e.g. a hand reaching into a pot on a grill).
REJECT = _px.REJECT + ["hand", "hands", "finger", "fingers", "arm", "wrist"]

COOKED = ["dish","plate","plated","bowl","meal","served","serving","cooked","roasted","baked",
    "grilled","fried","stew","soup","curry","casserole","gourmet","cuisine","dinner","lunch",
    "breakfast","homemade","delicious","tasty","recipe","garnish","sauce","dessert","cake","pie",
    "pastry","salad","noodles","pasta","platter","restaurant","appetizer","brunch","toast",
    "sandwich","pizza","stir-fry","barbecue","grill","roast","fried rice","risotto","paella"]
RAW = ["raw","uncooked","ingredient","ingredients","butcher","flesh","market","grocery","produce",
    "farm","harvest","slaughter","carcass","meat cuts","raw meat","uncooked meat","chopping","peeling"]
STOP = {"and","with","the","of","a","in","on","food","dish","style","homemade","recipe","minute"}

def words(tags):
    w = set()
    for t in tags:
        for x in t.split():
            w.add(x)
    return w

def has(keys, tagwords, full):
    for k in keys:
        if " " in k:
            if k in full:
                return True
        elif k in tagwords:
            return True
    return False

def count(keys, tagwords, full):
    n = 0
    for k in keys:
        if (k in full) if " " in k else (k in tagwords):
            n += 1
    return n

def score(tags, query):
    tw = words(tags)
    full = " ".join(tags)
    if has(REJECT, tw, full):
        return None                                   # scenery/animal/person → drop
    cooked = count(COOKED, tw, full)
    raw = count(RAW, tw, full)
    if raw and not cooked:
        return None                                   # pure raw/prep shot → drop
    qwords = [q for q in query.split() if q not in STOP]
    relevance = sum(1 for q in qwords if q in tw)
    head_bonus = 4 if qwords and qwords[-1] in tw else 0   # the dish noun (e.g. "stew")
    # relevance dominates so an on-topic dish (a real stew) beats a generic finished pie
    return 3 * relevance + head_bonus + min(cooked, 3) - 2 * min(raw, 2)

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
                if e.code in (400, 401): raise SystemExit(f"key rejected ({e.code})")
                return None
            except Exception:
                time.sleep(3); continue
        return None
    base = {"key": KEY, "image_type": "photo", "per_page": "150", "safesearch": "true"}
    js = call(dict(base, q=query[:100], category="food"))
    if not js or js.get("totalHits", 0) < 5:
        js = call(dict(base, q=query[:100])) or js
    time.sleep(pace)
    if not js:
        return []
    # store raw candidates (id + url + tags). id is Pixabay's STABLE per-image key —
    # the webformatURL hash differs across searches for the SAME photo, so we must
    # dedup by id, not url. scoring happens at assignment so it's tunable.
    out = []
    for h in js.get("hits", []):
        url = h.get("webformatURL")
        if url and h.get("id"):
            out.append({"id": h["id"], "u": url,
                        "t": [t.strip() for t in (h.get("tags") or "").lower().split(",") if t.strip()]})
    return out

def ranked(pool, query):
    """Return [(pixabay_id, url), …] best finished-dish first."""
    scored = []
    for c in pool:
        s = score(c["t"], query)
        if s is not None:
            scored.append((s, c["id"], c["u"]))
    scored.sort(key=lambda x: -x[0])
    return [(cid, u) for _, cid, u in scored]

def main():
    if not KEY:
        raise SystemExit("Set PIXABAY_API_KEY env var first.")
    pace = float(sys.argv[1]) if len(sys.argv) > 1 else 0.8
    rescore_only = len(sys.argv) > 2 and sys.argv[2] == "rescore"

    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    for r in recipes:
        r["_q"] = query_for(r["name"])

    def is_wiki(r):
        u = r.get("imageUrl")
        return u and urlparse(u).netloc == "upload.wikimedia.org"

    in_scope = [r for r in recipes if not is_wiki(r)]
    scope = {r["name"] for r in in_scope}
    queries = sorted({r["_q"] for r in in_scope})
    print(f"audit: {len(in_scope)} recipes ({len(recipes)-len(in_scope)} wiki kept), "
          f"{len(queries)} queries (~{len(queries)*pace/60:.0f} min)", flush=True)

    pools = json.loads(POOLS.read_text(encoding="utf-8")) if POOLS.exists() else {}
    if not rescore_only:
        for i, q in enumerate(queries):
            if q not in pools:
                pools[q] = fetch_pool(q, pace)
                if (i + 1) % 25 == 0:
                    POOLS.write_text(json.dumps(pools), encoding="utf-8")
                    print(f"  {i+1}/{len(queries)}", flush=True)
        POOLS.write_text(json.dumps(pools), encoding="utf-8")

    # dedup by Pixabay image id (the webformatURL is NOT a stable per-image key —
    # the same photo comes back under different /get/ hashes across searches).
    used_ids = set()
    assigned = kept = 0
    for r in in_scope:
        order = ranked(pools.get(r["_q"]) or [], r["_q"])
        pick = next(((cid, u) for cid, u in order if cid not in used_ids), None)
        if pick:
            r["imageUrl"] = pick[1]; used_ids.add(pick[0]); assigned += 1
        elif r.get("imageUrl"):
            kept += 1

    for r in recipes:
        r.pop("_q", None)
    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)

    ids = Counter(r["imageUrl"] for r in recipes if r.get("imageUrl"))
    shared = sum(v for v in ids.values() if v > 1)
    hosts = Counter(urlparse(r["imageUrl"]).netloc for r in recipes if r.get("imageUrl"))
    print(f"assigned {assigned}, kept {kept}. distinct={len(ids)} shared={shared} "
          f"({100*shared/len(recipes):.1f}%)", flush=True)
    for h, c in sorted(hosts.items(), key=lambda x: -x[1]):
        print(f"  {c:5}  {h}", flush=True)
    print(f"schemaVersion={b['schemaVersion']}", flush=True)

if __name__ == "__main__":
    main()

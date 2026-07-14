"""
Merge the two Pexels caches into the shipped bundle, in priority order:
  1. pexels_cache.json     -> fills recipes that had no image
  2. descenery_cache.json  -> OVERWRITES place-named recipes with a food-biased
                              photo (fixes Austrian=mountain, Buffalo=animal, ...)
Verified Wikibooks photos already in the bundle are left untouched.

Bumps schemaVersion and prints coverage. DB-free. Run after both passes finish.
Pass the new migration marker as argv[1] to print it for the C# side; the marker
itself lives in DatabaseService.cs.
"""
import gzip, json, sys
from pathlib import Path
from urllib.parse import urlparse

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"

def load(name):
    p = HERE / name
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else {}

def main():
    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    pex = load("pexels_cache.json")
    desc = load("descenery_cache.json")

    filled = overwritten = 0
    for r in recipes:
        name = r["name"]
        if not r.get("imageUrl") and pex.get(name):
            r["imageUrl"] = pex[name]; filled += 1
        if desc.get(name):
            r["imageUrl"] = desc[name]; overwritten += 1

    hosts = {}
    for r in recipes:
        u = r.get("imageUrl")
        h = urlparse(u).netloc if u else "(none)"
        hosts[h] = hosts.get(h, 0) + 1
    total = sum(1 for r in recipes if r.get("imageUrl"))

    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)

    print(f"filled {filled} imageless, overwrote {overwritten} place-named")
    for h, c in sorted(hosts.items(), key=lambda x: -x[1]):
        print(f"  {c:5}  {h}")
    print(f"total with image: {total}/{len(recipes)} ({100*total/len(recipes):.1f}%)")
    print(f"wrote bundle schemaVersion={b['schemaVersion']}")
    if len(sys.argv) > 1:
        print(f"set RecipeImagesMarker = \"{sys.argv[1]}\"")

if __name__ == "__main__":
    main()

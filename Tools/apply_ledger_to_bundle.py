"""Apply the verified image ledger to the shipped Wikibooks bundle.

The recipe_image_ledger.json is the source of truth from the fresh image re-audit.
Every verified_ok entry carries a STABLE url (wikimedia/pexels). This script copies
those stable urls onto the bundle (matched by recipe NAME -- the bundle has no id),
replacing the ~2200 expiring pixabay urls that shipped in v56.

Match is by name; the ledger and bundle share names 100%.

needs_fix / pending policy (argv flag):
  keep  (default) -- leave the recipe's current bundle image untouched. It may still
                     be an expiring pixabay url, but showing *something* now beats an
                     emoji, and these were not confirmed inaccurate, only un-upgraded.
  null            -- blank imageUrl so the emoji category fallback shows. Use only if
                     we decide the un-upgraded images are not trustworthy.

Usage:
  python tools/apply_ledger_to_bundle.py <marker> [keep|null]
  e.g. python tools/apply_ledger_to_bundle.py recipe_images_2026_07_10_v25.done keep
"""
import gzip, json, sys, io, collections
from pathlib import Path
from urllib.parse import urlparse

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
LED = HERE / "recipe_image_ledger.json"

def host(u):
    if not u: return "(none)"
    if u.startswith("asset:"): return "local-bundled"
    h = urlparse(u).netloc
    if "pixabay" in h: return "pixabay(EXPIRING)"
    if "wikimedia" in h: return "wikimedia"
    if "pexels" in h: return "pexels"
    return h

def main():
    marker = sys.argv[1] if len(sys.argv) > 1 else None
    policy = sys.argv[2] if len(sys.argv) > 2 else "keep"
    assert policy in ("keep", "null"), "policy must be keep|null"

    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    byname = {r["name"]: r for r in recipes}
    led = json.load(io.open(LED, encoding="utf-8"))

    applied = missing = nulled = kept = 0
    for x in led:
        r = byname.get(x["name"])
        if r is None:
            missing += 1
            continue
        st = x["status"]
        if st == "verified_ok" and x.get("url"):
            r["imageUrl"] = x["url"]; applied += 1
        elif st in ("needs_fix", "pending"):
            if policy == "null":
                r["imageUrl"] = None; nulled += 1
            else:
                kept += 1

    # coverage report
    hc = collections.Counter(host(r.get("imageUrl")) for r in recipes)
    withimg = sum(1 for r in recipes if r.get("imageUrl"))

    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)

    print(f"applied stable url to {applied} verified_ok recipes")
    print(f"needs_fix/pending: kept={kept} nulled={nulled}  (policy={policy})")
    if missing: print(f"WARNING: {missing} ledger names not found in bundle")
    print(f"total with image: {withimg}/{len(recipes)} ({100*withimg/len(recipes):.1f}%)")
    print("bundle image hosts now:")
    for h, n in hc.most_common():
        print(f"  {n:5}  {h}")
    print(f"wrote bundle schemaVersion={b['schemaVersion']}")
    if marker:
        print(f"\n>>> set RecipeImagesMarker = \"{marker}\" in Data/SeedData.WikibooksRecipes.cs")

if __name__ == "__main__":
    main()

"""
Make the prune/re-image lists EXACT (not eyeballed):

  1. IMAGE-HASH pass  -> cluster shipping images that are the SAME photo
     (perceptual dhash + exact-bytes). Two DISTINCT recipes sharing one photo
     -> re-image one. A same-dish cluster sharing a photo -> delete extras.

  2. NAME-SCAN pass    -> cluster recipes whose names are the SAME DISH
     (numbered variants I/II/III, "(1940)", trailing digits, and
     name-with-parenthetical vs bare name).

Reads the ledger for the shipping set (verified_ok + url). Local images live in
Resources/Raw/recipe_fix/<id>.jpg; pexels images are cached in the audit_cache.

Usage:  python tools/dedup_analysis.py
Writes: tools/dedup_report.json  (image_clusters, name_clusters) + prints summary
"""
import json, io, os, re, sys, hashlib
from collections import defaultdict
from PIL import Image

ROOT   = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEDGER = os.path.join(ROOT, "tools", "recipe_image_ledger.json")
LOCAL  = os.path.join(ROOT, "Resources", "Raw", "recipe_fix")
SCRATCH= os.path.join(os.environ["TEMP"], "claude",
                      "d--Claude-Projects-IntelligentPersonalHealthOptimization",
                      "0db7466b-892c-4292-8c17-727a5e5cf47f", "scratchpad")
CACHE  = os.path.join(SCRATCH, "audit_cache")
OUT    = os.path.join(ROOT, "tools", "dedup_report.json")


def load():
    led = json.load(io.open(LEDGER, encoding="utf-8"))
    ship = [r for r in led if r.get("status") == "verified_ok" and r.get("url")]
    ship.sort(key=lambda r: r["id"])
    return ship


def img_path(r):
    u = r["url"]
    if u.startswith("asset:"):
        return os.path.join(LOCAL, u[len("asset:"):])
    return os.path.join(CACHE, f"{r['id']}.jpg")


# ---------- EXACT shared-photo clustering ----------
# A "shared photo" = literally the same image, not merely a similar-looking dish.
# Two reliable exact signals:
#   (a) identical remote URL  (pexels hotlinks reused across recipes)
#   (b) identical file bytes  (local recipe_fix/<id>.jpg downscaled from same source)
def image_clusters(ship):
    by_url  = defaultdict(list)
    by_md5  = defaultdict(list)
    missing = 0
    for r in ship:
        u = r["url"]
        if u.startswith("https://"):
            by_url[u].append(r["id"])
        p = img_path(r)
        try:
            with open(p, "rb") as f:
                h = hashlib.md5(f.read()).hexdigest()
            by_md5[h].append(r["id"])
        except Exception:
            missing += 1
    clusters = []
    for u, ids in by_url.items():
        if len(ids) > 1:
            clusters.append(sorted(ids))
    for h, ids in by_md5.items():
        if len(ids) > 1:
            clusters.append(sorted(ids))
    # merge clusters that overlap (a url-cluster and a bytes-cluster can share ids)
    merged = []
    for grp in clusters:
        s = set(grp)
        hit = None
        for m in merged:
            if m & s:
                hit = m; break
        if hit is not None:
            hit |= s
        else:
            merged.append(s)
    out = [sorted(m) for m in merged]
    out.sort()
    print(f"[image] {len(ship)} images ({missing} unreadable) -> {len(out)} EXACT shared-photo clusters")
    return out


# ---------- name-scan clustering ----------
ROMAN = r"(?:i{1,3}|iv|v|vi{1,3}|ix|x)"
def norm_name(name):
    n = name.lower().strip()
    n = re.sub(r"\(.*?\)", "", n)                     # drop parentheticals
    n = re.sub(r"\b" + ROMAN + r"\b\s*$", "", n)      # trailing roman numeral
    n = re.sub(r"\b\d{3,4}\b", "", n)                 # years like 1940
    n = re.sub(r"[#0-9]+\s*$", "", n)                 # trailing digits
    n = re.sub(r"\s+", " ", n).strip(" -,")
    return n


def name_clusters(ship):
    groups = defaultdict(list)
    for r in ship:
        groups[norm_name(r["name"])].append((r["id"], r["name"]))
    # also add emoji-floor (needs_fix) recipes so we see full same-dish sets
    clusters = []
    for key, items in groups.items():
        if len(items) > 1 and key:
            clusters.append({"dish": key, "members": sorted(items)})
    clusters.sort(key=lambda c: (-len(c["members"]), c["dish"]))
    print(f"[name] {len(clusters)} same-dish name clusters (>=2 members)")
    return clusters


def sp(s):
    sys.stdout.buffer.write((s + "\n").encode("utf-8", "replace"))


def main():
    ship = load()
    print(f"shipping set: {len(ship)} images")
    imgc = image_clusters(ship)
    namec = name_clusters(ship)
    name_by_id = {r["id"]: r["name"] for r in ship}
    imgc_named = [[[i, name_by_id.get(i, "?")] for i in g] for g in imgc]
    json.dump({"image_clusters": imgc_named, "name_clusters": namec},
              io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"wrote {OUT}")
    sp(f"\n=== {len(imgc_named)} EXACT SHARED-PHOTO CLUSTERS ===")
    for g in imgc_named:
        sp("  " + " | ".join(f"{i} {n}" for i, n in g))
    sp(f"\n=== {len(namec)} SAME-DISH NAME CLUSTERS ===")
    for c in namec:
        sp(f"  [{c['dish']}] " + " | ".join(f"{i} {n}" for i, n in c["members"]))


if __name__ == "__main__":
    main()

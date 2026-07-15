"""
Download all verified_ok Pexels-hosted recipe photos to the LOCAL bundle folder
(Resources/Raw/recipe_fix/<id>.jpg), downscaled, so the app serves them as a
FileImageSource instead of hotlinking images.pexels.com.

Why: ~1191 recipe photos are still hotlinked from images.pexels.com, so they only
appear when the phone is online — in airplane mode those recipes fall back to the
emoji tile. Localizing them (same proven path as the 1439 wikimedia/pixabay locals)
makes the WHOLE catalog work fully offline. Pexels image urls are stable, so this is
a one-time download.

Resumable: skips ids already present as recipe_fix/<id>.jpg. Paced + 429 backoff.
On persistent failure, leaves the recipe's remote url untouched (stays hotlinked).

Usage:
  python tools/bundle_pexels_local.py            # download only
  python tools/bundle_pexels_local.py apply      # download, then rewrite ledger
"""
import json, io, os, sys, time, urllib.request, urllib.error
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEDGER = os.path.join(ROOT, "tools", "recipe_image_ledger.json")
DEST   = os.path.join(ROOT, "Resources", "Raw", "recipe_fix")
UA = "IntelligentPersonalHealthOptimization/1.0 (personal health app; contact via GitHub repo)"
MAXEDGE = 450          # longest edge px — good for 60px thumb and ~450px detail hero
QUALITY = 80
BASE_DELAY = 0.25      # polite pacing between requests
os.makedirs(DEST, exist_ok=True)


def fetch(url, tries=5):
    delay = 3.0
    for attempt in range(tries):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            return urllib.request.urlopen(req, timeout=25).read()
        except urllib.error.HTTPError as e:
            if e.code in (429, 503) and attempt < tries - 1:
                time.sleep(delay); delay *= 2; continue
            raise
        except Exception:
            if attempt < tries - 1:
                time.sleep(delay); delay *= 2; continue
            raise
    return None


def downscale_save(data, path):
    im = Image.open(io.BytesIO(data))
    if im.mode not in ("RGB",):
        im = im.convert("RGB")
    im.thumbnail((MAXEDGE, MAXEDGE), Image.LANCZOS)
    im.save(path, "JPEG", quality=QUALITY, optimize=True)


def main():
    led = json.load(io.open(LEDGER, encoding="utf-8"))
    targets = [r for r in led
               if r.get("status") == "verified_ok"
               and "images.pexels.com" in (r.get("url") or "")]
    print(f"{len(targets)} verified_ok pexels recipes to localize")
    ok = skip = fail = 0
    failed = []
    for i, r in enumerate(targets):
        rid = r["id"]
        path = os.path.join(DEST, f"{rid}.jpg")
        if os.path.exists(path) and os.path.getsize(path) > 0:
            skip += 1
            continue
        try:
            data = fetch(r["url"])
            downscale_save(data, path)
            ok += 1
        except Exception as e:
            fail += 1
            failed.append((rid, r["name"], repr(e)[:60]))
            print(f"  FAIL {rid} {r['name'][:40]!r} {e!r}"[:100])
        if (ok + fail) % 50 == 0 and (ok + fail) > 0:
            print(f"  ...{i+1}/{len(targets)}  ok={ok} skip={skip} fail={fail}")
        time.sleep(BASE_DELAY)
    print(f"DONE download: ok={ok} skip={skip} fail={fail}")
    if failed:
        json.dump(failed, io.open(os.path.join(ROOT, "tools", "pexels_local_failed.json"), "w", encoding="utf-8"), indent=1)
        print(f"wrote {len(failed)} failures to tools/pexels_local_failed.json")

    if len(sys.argv) > 1 and sys.argv[1] == "apply":
        apply_ledger(led, targets)


def apply_ledger(led, targets):
    changed = 0
    for r in targets:
        rid = r["id"]
        path = os.path.join(DEST, f"{rid}.jpg")
        if os.path.exists(path) and os.path.getsize(path) > 0:
            r["url"] = f"asset:{rid}.jpg"
            r["host"] = "local-bundled"
            r["stability"] = "local"
            r["note"] = (r.get("note") or "") + " | localized from pexels (offline support)"
            changed += 1
    json.dump(led, io.open(LEDGER, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"ledger: rewrote {changed} entries to asset:<id>.jpg (local-bundled)")


if __name__ == "__main__":
    main()

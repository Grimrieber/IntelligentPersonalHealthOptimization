"""
Re-image pipeline, step 3: apply my verified picks.

Reads scratchpad/reimg_picks.json  { "<id>": "px3" | "pe0" | "wm2" | "NONE", ... }
where the value is the winning candidate label from the montage board, or "NONE"
if no candidate was an accurate photo of the dish.

For each pick:
  * localizes the chosen candidate (scratchpad/reimg_cand/<id>_<label>.jpg) into
    Resources/Raw/recipe_fix/<id>.jpg (downscaled 450px, q80) so it is stable,
    offline, and never throttles — then sets the ledger entry to asset:<id>.jpg.
  * "NONE"  -> ledger status=needs_fix, url=null  (emoji fallback; accurate-nothing
    beats a wrong photo).

Does NOT touch the bundle directly — run apply_ledger_to_bundle.py afterwards so the
whole ledger (deletes + re-images) lands in one bundle rewrite.

Usage:  python tools/reimage_apply.py            # dry run
        python tools/reimage_apply.py apply
"""
import json, io, os, sys
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
SC = Path("C:/Users/Stephan/AppData/Local/Temp/claude/"
          "d--Claude-Projects-IntelligentPersonalHealthOptimization/"
          "0db7466b-892c-4292-8c17-727a5e5cf47f/scratchpad")
PICKS = SC / "reimg_picks.json"
CAND = SC / "reimg_cand"
LED = HERE / "recipe_image_ledger.json"
DEST = HERE.parent / "Resources" / "Raw" / "recipe_fix"
MAXEDGE, QUALITY = 450, 80


def localize(src_path, dest_path):
    im = Image.open(src_path)
    if im.mode != "RGB":
        im = im.convert("RGB")
    im.thumbnail((MAXEDGE, MAXEDGE), Image.LANCZOS)
    im.save(dest_path, "JPEG", quality=QUALITY, optimize=True)


def main():
    apply = len(sys.argv) > 1 and sys.argv[1] == "apply"
    picks = json.load(io.open(PICKS, encoding="utf-8"))
    led = json.load(io.open(LED, encoding="utf-8"))
    byid = {r["id"]: r for r in led}
    ok = none = miss = 0
    for rid_s, label in picks.items():
        rid = int(rid_s)
        r = byid.get(rid)
        if r is None:
            miss += 1; continue
        if label == "NONE":
            none += 1
            if apply:
                r["url"] = None; r["host"] = "(none)"; r["stability"] = "emoji"
                r["status"] = "needs_fix"; r["note"] = "reimage: no accurate candidate -> emoji"
            continue
        src = CAND / f"{rid_s}_{label}.jpg"
        if not (src.exists() and src.stat().st_size > 800):
            miss += 1
            print(f"  MISSING candidate {rid_s} {label}")
            continue
        ok += 1
        if apply:
            localize(src, DEST / f"{rid}.jpg")
            r["url"] = f"asset:{rid}.jpg"; r["host"] = "local-bundled"
            r["stability"] = "local"; r["status"] = "verified_ok"
            r["note"] = f"reimage: unique photo ({label}), localized"
    print(f"picks={len(picks)}  localize={ok}  none={none}  missing={miss}")
    if apply:
        json.dump(led, io.open(LED, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        print(f"WROTE ledger ({len(led)} entries)")
    else:
        print("DRY RUN — pass 'apply' to write.")


if __name__ == "__main__":
    main()

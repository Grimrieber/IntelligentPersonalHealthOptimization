"""Record rehunt board picks into recipe_image_ledger.json.

  python tools/rehunt_record.py '{"123":"p2","456":"x0","789":"c1","999":null}'

Value semantics:
  "p<j>" / "c<j>"  -> Pexels/Commons candidate. Stored as its STABLE remote url
                      (images.pexels.com / upload.wikimedia.org). No bundling needed.
  "x<j>"           -> Pixabay candidate. Pixabay hotlinks EXPIRE, so we DOWNLOAD the
                      image to Resources/Raw/recipe_fix/<id>.jpg (a MauiAsset) and store
                      url = "asset:<id>.jpg". The app copies it to local storage at
                      migration and binds Image.Source to the local file (never rots).
  null/""/"x"      -> leave needs_fix (no accurate candidate).

Idempotent per id. Only stable remote hosts or local asset refs are ever written.
"""
import json, sys, io, urllib.request
from pathlib import Path
from urllib.parse import urlparse
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
LED = ROOT / "tools" / "recipe_image_ledger.json"
POOL = ROOT / "tools" / "rehunt_pool.json"
ASSETS = ROOT / "Resources" / "Raw" / "recipe_fix"; ASSETS.mkdir(parents=True, exist_ok=True)
UA = "HealthOptimizerRecipeImages/1.0 (dish photo lookup; jamesdmiller9@gmail.com)"
STABLE = ("upload.wikimedia.org", "images.pexels.com")
SRC = {"c": "commons", "p": "pexels", "x": "pixabay"}

def fetch(url, dest):
    for _ in range(4):
        try:
            d = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": UA}), timeout=30).read()
            if len(d) > 800:
                dest.write_bytes(d); return True
        except Exception: pass
    return False

def main():
    picks = json.loads(sys.argv[1])
    led = json.loads(LED.read_text(encoding="utf-8"))
    pool = json.loads(POOL.read_text(encoding="utf-8"))
    byid = {str(x["id"]): x for x in led}
    ok = miss = bundled = fail = 0
    for rid, val in picks.items():
        x = byid.get(str(rid))
        if not x: print(f"  ?? id {rid} not in ledger"); continue
        if val in (None, "", "x", "null"):
            x["status"] = "needs_fix"; x["url"] = None; miss += 1; continue
        src = SRC.get(val[0]); j = int(val[1:])
        cands = pool.get(str(rid), {}).get(src, [])
        if j >= len(cands):
            print(f"  !! id {rid} {val} out of range ({len(cands)} {src})"); fail += 1; continue
        url = cands[j][0]
        if src == "pixabay":
            dest = ASSETS / f"{rid}.jpg"
            if not fetch(url, dest):
                print(f"  !! id {rid} pixabay download failed"); fail += 1; continue
            x["url"] = f"asset:{rid}.jpg"; x["host"] = "local-bundled"; bundled += 1
        else:
            host = urlparse(url).netloc
            assert any(s in host for s in STABLE), f"UNSTABLE {rid}: {host}"
            x["url"] = url; x["host"] = "wikimedia" if "wikimedia" in host else "pexels"
        x["stability"] = "stable"; x["status"] = "verified_ok"; x["note"] = f"rehunt {val}"
        ok += 1
    LED.write_text(json.dumps(led, ensure_ascii=False, indent=0), encoding="utf-8")
    vok = sum(1 for x in led if x["status"] == "verified_ok")
    nf = sum(1 for x in led if x["status"] == "needs_fix")
    print(f"recorded {ok} verified ({bundled} bundled pixabay), {miss} needs, {fail} fail; "
          f"ledger verified_ok={vok} needs_fix={nf}")

if __name__ == "__main__":
    main()

"""
Full visual audit: render EVERY shipping recipe image into numbered montage
boards so each can be eyeballed for wrong-dish / violation / duplicate.

Source of truth = tools/recipe_image_ledger.json (id, name, url).
  asset:<id>.jpg  -> read local Resources/Raw/recipe_fix/<id>.jpg
  https pexels    -> download to scratchpad cache (Pexels CDN, no throttle)
  needs_fix/none  -> skipped (the 108 emoji floor; listed separately)

Boards: 30 images each (6x5), sorted by id (deterministic), labeled "id name".

Usage:
  python tools/full_audit_montage.py cache     # download pexels to cache
  python tools/full_audit_montage.py boards     # build all boards
  python tools/full_audit_montage.py both       # cache then boards
"""
import json, io, os, sys, time, urllib.request
from concurrent.futures import ThreadPoolExecutor
from PIL import Image, ImageDraw, ImageFont

ROOT   = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEDGER = os.path.join(ROOT, "tools", "recipe_image_ledger.json")
LOCAL  = os.path.join(ROOT, "Resources", "Raw", "recipe_fix")
SCRATCH= os.path.join(os.environ["TEMP"], "claude",
                      "d--Claude-Projects-IntelligentPersonalHealthOptimization",
                      "1050c120-1b7c-4cb0-af2c-c10e61b4971f", "scratchpad")
CACHE  = os.path.join(SCRATCH, "audit_cache")
BOARDS = os.path.join(SCRATCH, "audit_boards")
UA = "IntelligentPersonalHealthOptimization/1.0 (personal health app)"
PERBOARD = 30
COLS, ROWS = 6, 5
CELL_W, CELL_H = 250, 250
IMG_H = 200
os.makedirs(CACHE, exist_ok=True)
os.makedirs(BOARDS, exist_ok=True)


def load():
    led = json.load(io.open(LEDGER, encoding="utf-8"))
    ship = [r for r in led if r.get("status") == "verified_ok" and r.get("url")]
    ship.sort(key=lambda r: r["id"])
    return ship


def pexels_items(ship):
    return [r for r in ship if (r.get("url") or "").startswith("https://images.pexels.com")]


def remote_items(ship):
    # every non-local (http/https) image, regardless of host — pexels, pixabay, wikimedia
    return [r for r in ship if (r.get("url") or "").startswith("http")]


def cache_one(r):
    dest = os.path.join(CACHE, f"{r['id']}.jpg")
    if os.path.exists(dest) and os.path.getsize(dest) > 0:
        return True
    try:
        req = urllib.request.Request(r["url"], headers={"User-Agent": UA})
        data = urllib.request.urlopen(req, timeout=25).read()
        with open(dest, "wb") as f:
            f.write(data)
        return True
    except Exception as e:
        print(f"  FAIL cache {r['id']} {r['name'][:30]!r} {e!r}"[:90])
        return False


def cmd_cache(ship):
    rem = remote_items(ship)
    print(f"caching {len(rem)} remote images (pexels+pixabay+wiki)...")
    ok = 0
    with ThreadPoolExecutor(max_workers=6) as ex:
        for got in ex.map(cache_one, rem):
            ok += 1 if got else 0
    print(f"cached {ok}/{len(rem)} remote")


def img_path(r):
    u = r["url"]
    if u.startswith("asset:"):
        return os.path.join(LOCAL, u[len("asset:"):])
    return os.path.join(CACHE, f"{r['id']}.jpg")


def cmd_boards(ship):
    try:
        font = ImageFont.truetype("arial.ttf", 13)
    except Exception:
        font = ImageFont.load_default()
    n = len(ship)
    nboards = (n + PERBOARD - 1) // PERBOARD
    print(f"{n} images -> {nboards} boards")
    missing = []
    for b in range(nboards):
        chunk = ship[b * PERBOARD:(b + 1) * PERBOARD]
        board = Image.new("RGB", (COLS * CELL_W, ROWS * CELL_H), (18, 18, 22))
        d = ImageDraw.Draw(board)
        for i, r in enumerate(chunk):
            x = (i % COLS) * CELL_W
            y = (i // COLS) * CELL_H
            p = img_path(r)
            try:
                im = Image.open(p).convert("RGB")
                im.thumbnail((CELL_W - 8, IMG_H))
                board.paste(im, (x + 4, y + 2))
            except Exception:
                missing.append((r["id"], r["name"]))
                d.rectangle([x + 4, y + 2, x + CELL_W - 4, y + IMG_H], outline=(200, 60, 60))
                d.text((x + 10, y + 80), "MISSING", fill=(220, 80, 80), font=font)
            label = f"{r['id']} {r['name']}"
            d.text((x + 4, y + IMG_H + 6), label[:34], fill=(235, 235, 235), font=font)
            if len(label) > 34:
                d.text((x + 4, y + IMG_H + 22), label[34:68], fill=(235, 235, 235), font=font)
        out = os.path.join(BOARDS, f"board_{b:03d}.png")
        board.save(out)
    print(f"wrote {nboards} boards to {BOARDS}")
    if missing:
        print(f"WARNING {len(missing)} images could not be loaded:")
        for mid, mn in missing[:20]:
            print("  ", mid, mn[:40])


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "both"
    ship = load()
    if cmd in ("cache", "both"):
        cmd_cache(ship)
    if cmd in ("boards", "both"):
        cmd_boards(ship)

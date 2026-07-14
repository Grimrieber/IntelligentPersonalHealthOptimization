"""Post-Pexels-pool prep: for every non-verified target now carrying Pexels candidates,
delete any STALE downloaded jpgs (old Commons picks) so download() re-fetches the actual
Pexels image, then build filtered montage boards of ONLY these recipes for review.

Run AFTER `poolpexels` finishes and AFTER `download`:
  python tools/phaseb_pexels_prep.py clean      # delete stale jpgs for pexels targets
  python tools/phaseb_fetch.py download          # fetch fresh pexels images
  python tools/phaseb_pexels_prep.py montage     # build image_archive/_phaseb/mon3/*.png
"""
import json, sys, io
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
LED = ROOT / "tools" / "recipe_image_ledger.json"
POOL = ROOT / "tools" / "phaseb_pool.json"
PB = ROOT / "image_archive" / "_phaseb"
MON3 = PB / "mon3"; MON3.mkdir(parents=True, exist_ok=True)

def load(): return json.loads(LED.read_text(encoding="utf-8"))
def loadpool(): return json.loads(POOL.read_text(encoding="utf-8"))

def pexels_targets():
    led = load(); pool = loadpool()
    ids = [x["id"] for x in led if x["status"] in ("pending","needs_fix")
           and pool.get(str(x["id"]),{}).get("src")=="pexels"
           and pool.get(str(x["id"]),{}).get("cands")]
    return sorted(ids)

def clean():
    ids = pexels_targets()
    removed = 0
    for rid in ids:
        for p in PB.glob(f"{rid}_*.jpg"):
            p.unlink(); removed += 1
    print(f"pexels targets={len(ids)}; removed {removed} stale jpgs (download will refetch)", flush=True)

def montage(per=10):
    pool = loadpool()
    ids = [i for i in pexels_targets()
           if any((PB / f"{i}_{j}.jpg").exists() for j in range(len(pool[str(i)]["cands"])))]
    try: font = ImageFont.truetype("arial.ttf", 12); fb = ImageFont.truetype("arialbd.ttf", 14)
    except Exception: font = fb = ImageFont.load_default()
    def asc(s): return s.encode("ascii","replace").decode()
    CELL, LBL, COLS = 235, 250, 3
    n = (len(ids)+per-1)//per
    for m in range(n):
        batch = ids[m*per:(m+1)*per]
        mo = Image.new("RGB", (LBL+CELL*COLS, CELL*len(batch)), (16,16,20)); d = ImageDraw.Draw(mo)
        for r, rid in enumerate(batch):
            y = r*CELL; v = pool[str(rid)]
            d.text((5, y+6), str(rid), fill=(120,220,255), font=fb)
            words = asc(v["name"]).split(); line=""; yy=y+26
            for w in words:
                if len(line)+len(w) > 28: d.text((6,yy),line,fill=(255,255,255),font=font); yy+=16; line=w
                else: line=(line+" "+w).strip()
            d.text((6,yy),line,fill=(255,255,255),font=font)
            d.text((6,y+CELL-18), "q: "+asc(v["q"])[:30], fill=(150,150,150), font=font)
            for j in range(len(v["cands"])):
                x = LBL + j*CELL; p = PB / f"{rid}_{j}.jpg"
                if p.exists():
                    try: mo.paste(Image.open(p).convert("RGB").resize((CELL,CELL)), (x, y))
                    except Exception: pass
                d.rectangle([x, y, x+20, y+16], fill=(0,0,0))
                d.text((x+3, y+1), str(j), fill=(255,255,0), font=fb)
        mo.save(MON3 / f"{m:04d}.png")
    print(f"wrote {n} montages to image_archive/_phaseb/mon3/ ({len(ids)} recipes)", flush=True)

if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "montage"
    {"clean": clean, "montage": lambda: montage(int(sys.argv[2]) if len(sys.argv)>2 else 10)}[cmd]()

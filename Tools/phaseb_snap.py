"""Targeted download + montage for a FROZEN snapshot of Pexels-pooled recipe ids.
Safe to run while poolpexels writes phaseb_pool.json for OTHER ids (we only touch the
snapshot ids, whose pool entries are stable).

  python tools/phaseb_snap.py dl   tools/snapshot_pexels286.json
  python tools/phaseb_snap.py mont tools/snapshot_pexels286.json [per]
"""
import json, sys, io, time, urllib.request
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
POOL = ROOT / "tools" / "phaseb_pool.json"
PB = ROOT / "image_archive" / "_phaseb"
MON3 = PB / "mon3"; MON3.mkdir(parents=True, exist_ok=True)
UA = "HealthOptimizerRecipeImages/1.0 (dish photo lookup; jamesdmiller9@gmail.com)"

def loadpool():
    for _ in range(8):
        try: return json.loads(POOL.read_text(encoding="utf-8"))
        except Exception: time.sleep(0.5)
    raise SystemExit("pool unreadable")

def dl(snap):
    pool = loadpool()
    ok = 0; tot = 0
    for rid in snap:
        v = pool.get(str(rid), {})
        for j, c in enumerate(v.get("cands", [])):
            tot += 1
            p = PB / f"{rid}_{j}.jpg"
            # always refetch: a stale Commons jpg may sit here from a rejected pick
            got = False
            for _ in range(3):
                try:
                    d = urllib.request.urlopen(urllib.request.Request(c[0], headers={"User-Agent": UA}), timeout=20).read()
                    if len(d) > 500: p.write_bytes(d); got = True; break
                except Exception: time.sleep(1.0)
            if got: ok += 1
            time.sleep(0.15)
    print(f"downloaded {ok}/{tot} snapshot candidates", flush=True)

def mont(snap, per=10):
    pool = loadpool()
    ids = [i for i in snap if any((PB / f"{i}_{j}.jpg").exists() for j in range(len(pool[str(i)]["cands"])))]
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
    print(f"wrote {n} montages to mon3/ ({len(ids)} recipes)", flush=True)

if __name__ == "__main__":
    cmd = sys.argv[1]; snap = json.load(open(sys.argv[2]))
    if cmd == "dl": dl(snap)
    elif cmd == "mont": mont(snap, int(sys.argv[3]) if len(sys.argv) > 3 else 10)

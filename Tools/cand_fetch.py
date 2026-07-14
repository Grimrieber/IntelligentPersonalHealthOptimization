"""Fetch a candidate POOL (6 images) per flagged recipe using the crafted queries,
download them, and build row-montages (one recipe per row, 6 candidate columns) so
vision agents can pick the best finished-dish. No bundle writes.

Usage:
  fetch          : fetch 6 candidates/recipe -> cand/<gi>_<j>.jpg + cand_pool.json
  montage <PER>  : build row-montages, PER recipes each (default 8)
"""
import json, sys, io, urllib.request, concurrent.futures as cf
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import importlib.util
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
SC = Path("C:/Users/Stephan/AppData/Local/Temp/claude/d--Claude-Projects-IntelligentPersonalHealthOptimization/0db7466b-892c-4292-8c17-727a5e5cf47f/scratchpad")
spec = importlib.util.spec_from_file_location("vf", HERE / "vision_fix.py")
vf = importlib.util.module_from_spec(spec); spec.loader.exec_module(vf)
QUERIES = json.loads((SC / "crafted_queries.json").read_text(encoding="utf-8"))
_IDX = json.loads((SC / "audit_index.json").read_text(encoding="utf-8"))
NAMES = {str(i): _IDX[i]["name"] for i in range(len(_IDX))}
CD = SC / "cand"; CD.mkdir(exist_ok=True)

def fetch():
    used = set(json.loads(vf.LEDGER.read_text())) if vf.LEDGER.exists() else set()
    pool = {}
    gis = list(QUERIES.keys())
    for k, gi in enumerate(gis):
        cs = []
        try:
            for s, cid, url in vf.candidates(QUERIES[gi]):
                if cid in used: continue          # skip already-in-bundle ids
                cs.append([cid, url])
                if len(cs) >= 6: break
        except Exception as e:
            print("ERR", gi, type(e).__name__, flush=True)
        pool[gi] = cs
        if (k+1) % 25 == 0:
            print(f"{k+1}/{len(gis)}", flush=True)
            (SC / "cand_pool.json").write_text(json.dumps(pool, ensure_ascii=False), encoding="utf-8")
    (SC / "cand_pool.json").write_text(json.dumps(pool, ensure_ascii=False), encoding="utf-8")
    # download all
    jobs = [(gi, j, c[1]) for gi, cs in pool.items() for j, c in enumerate(cs)]
    def dl(t):
        gi, j, url = t
        p = CD / f"{gi}_{j}.jpg"
        if p.exists() and p.stat().st_size > 500: return
        try:
            data = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"}), timeout=30).read()
            if len(data) > 500: p.write_bytes(data)
        except Exception: pass
    with cf.ThreadPoolExecutor(max_workers=10) as ex: list(ex.map(dl, jobs))
    print(f"fetched pools for {len(pool)} recipes; {sum(len(v) for v in pool.values())} candidates", flush=True)

def montage(per):
    pool = json.loads((SC / "cand_pool.json").read_text())
    gis = list(pool.keys())
    try: font = ImageFont.truetype("arial.ttf", 13); fb = ImageFont.truetype("arialbd.ttf", 15)
    except Exception: font = ImageFont.load_default(); fb = font
    def ascii(s): return s.encode("ascii", "replace").decode()
    CELL, COLS = 220, 6
    MD = SC / "candmon"; MD.mkdir(exist_ok=True)
    nmon = (len(gis) + per - 1) // per
    for m in range(nmon):
        batch = gis[m*per:(m+1)*per]
        mo = Image.new("RGB", (CELL*COLS + 210, CELL*len(batch)), (18,18,22)); d = ImageDraw.Draw(mo)
        for r, gi in enumerate(batch):
            y = r*CELL
            d.text((4, y+4), f"{gi}", fill=(120,220,255), font=fb)
            d.text((4, y+24), ascii(NAMES.get(gi, ""))[:26], fill=(255,255,255), font=font)
            for j in range(len(pool[gi])):
                x = 210 + j*CELL; p = CD / f"{gi}_{j}.jpg"
                if p.exists():
                    try: mo.paste(Image.open(p).convert("RGB").resize((CELL,CELL)), (x, y))
                    except Exception: pass
                d.rectangle([x, y, x+22, y+18], fill=(0,0,0))
                d.text((x+3, y+2), f"{j}", fill=(255,255,0), font=fb)
        fn = MD / f"c{m:03d}.png"; mo.save(fn)
    print(f"wrote {nmon} montages to candmon/ (per={per})", flush=True)

if __name__ == "__main__":
    if sys.argv[1] == "fetch": fetch()
    else: montage(int(sys.argv[2]) if len(sys.argv) > 2 else 8)

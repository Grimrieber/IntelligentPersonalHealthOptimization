"""Fetch top candidates for the 4 stubborn recipes so I can hand-pick a correct
plated-dish URL, then patch proposal_refix2.json directly."""
import json, sys, urllib.request
from pathlib import Path
from PIL import Image, ImageDraw
import importlib.util

HERE = Path(__file__).resolve().parent
OUT = Path("C:/Users/Stephan/AppData/Local/Temp/claude/d--Claude-Projects-IntelligentPersonalHealthOptimization/0db7466b-892c-4292-8c17-727a5e5cf47f/scratchpad")
spec = importlib.util.spec_from_file_location("vf", HERE / "vision_fix.py")
vf = importlib.util.module_from_spec(spec); spec.loader.exec_module(vf)

# recipe index in proposal -> list of queries to pool candidates from
JOBS = {
 5:  ["pozole soup bowl", "hominy stew", "corn grits bowl"],
}

def build():
    prop = json.loads((HERE / "proposal_refix2.json").read_text())
    names = list(prop.keys())
    used = set(json.loads(vf.LEDGER.read_text())) if vf.LEDGER.exists() else set()
    D = OUT / "hp"; D.mkdir(exist_ok=True)
    cand = {}
    for idx, queries in JOBS.items():
        pool, seen = [], set()
        for q in queries:
            for s, cid, url in vf.candidates(q):
                if cid in used or cid in seen:
                    continue
                seen.add(cid); pool.append((cid, url))
                if len(pool) >= 8:
                    break
            if len(pool) >= 8:
                break
        cand[idx] = pool
        for j, (cid, url) in enumerate(pool):
            try:
                data = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"}), timeout=30).read()
                (D / f"{idx}_{j}.jpg").write_bytes(data)
            except Exception as e:
                print("dl err", idx, j, e)
    (HERE / "handpick_cand.json").write_text(json.dumps(cand))
    # montage: one row per recipe, 8 cols
    cell, cols = 230, 8
    rows = len(JOBS)
    mo = Image.new("RGB", (cols * cell, rows * cell), (25, 25, 30)); d = ImageDraw.Draw(mo)
    for r, idx in enumerate(JOBS):
        for j in range(8):
            p = D / f"{idx}_{j}.jpg"; x = j * cell; y = r * cell
            if p.exists():
                try:
                    im = Image.open(p).convert("RGB").resize((cell, cell)); mo.paste(im, (x, y))
                except Exception: pass
            d.rectangle([x, y, x + cell, y + 14], fill=(0, 0, 0))
            d.text((x + 2, y + 2), f"{idx}_{j} {names[idx][:20]}", fill=(255, 255, 0))
    f = OUT / "handpick.png"; mo.save(f); print("wrote", f)

def choose(picks):
    # picks: "idx:j,idx:j,..."
    prop = json.loads((HERE / "proposal_refix2.json").read_text())
    names = list(prop.keys())
    cand = {int(k): v for k, v in json.loads((HERE / "handpick_cand.json").read_text()).items()}
    for tok in picks.split(","):
        idx, j = map(int, tok.split(":"))
        cid, url = cand[idx][j]
        prop[names[idx]] = {"query": "handpick", "chosen": [cid, url], "alts": []}
        print(f"set {idx} {names[idx][:30]} -> {cid}")
    (HERE / "proposal_refix2.json").write_text(json.dumps(prop, indent=1))
    print("patched proposal_refix2.json")

if __name__ == "__main__":
    if sys.argv[1] == "build":
        build()
    else:
        choose(sys.argv[2])

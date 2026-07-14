"""
Re-image pipeline, step 2: build verification boards from the fetched candidate
pool (scratchpad/reimg_pool.json + scratchpad/reimg_cand/).

One recipe per ROW: left gutter = id + name + query; then each candidate as a
labeled column "src+index" (px0, pe1, wm2 …). I read each board and record the
winning candidate per recipe (or NONE) into reimg_picks.json.

Usage:  python tools/reimage_montage.py [PER]     # PER recipes/board (default 8)
"""
import json, io, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
SC = Path("C:/Users/Stephan/AppData/Local/Temp/claude/"
          "d--Claude-Projects-IntelligentPersonalHealthOptimization/"
          "0db7466b-892c-4292-8c17-727a5e5cf47f/scratchpad")
POOL = SC / "reimg_pool.json"
CAND = SC / "reimg_cand"
BOARDS = SC / "reimg_boards"; BOARDS.mkdir(exist_ok=True)
TARGETS = HERE / "reimage_targets.json"

CELL = 210
GUT = 250


def main():
    per = int(sys.argv[1]) if len(sys.argv) > 1 else 8
    pool = json.load(io.open(POOL, encoding="utf-8"))
    order = [str(t["id"]) for t in json.load(io.open(TARGETS, encoding="utf-8"))]
    order = [r for r in order if r in pool]
    try:
        font = ImageFont.truetype("arial.ttf", 13); fb = ImageFont.truetype("arialbd.ttf", 15)
    except Exception:
        font = ImageFont.load_default(); fb = font

    def ascii(s): return s.encode("ascii", "replace").decode()
    maxc = max((len(pool[r]["cands"]) for r in order), default=0)
    nb = (len(order) + per - 1) // per
    print(f"{len(order)} targets -> {nb} boards (per={per}, up to {maxc} cands)")
    for m in range(nb):
        batch = order[m * per:(m + 1) * per]
        W = GUT + maxc * CELL
        board = Image.new("RGB", (W, per * CELL), (18, 18, 22))
        d = ImageDraw.Draw(board)
        for r, rid in enumerate(batch):
            y = r * CELL
            info = pool[rid]
            d.text((4, y + 4), rid, fill=(120, 220, 255), font=fb)
            d.text((4, y + 24), ascii(info["name"])[:30], fill=(255, 255, 255), font=font)
            d.text((4, y + 42), "q:" + ascii(info["query"])[:28], fill=(160, 200, 160), font=font)
            for j, c in enumerate(info["cands"]):
                x = GUT + j * CELL
                p = CAND / f"{rid}_{c['src']}{j}.jpg"
                if p.exists():
                    try:
                        board.paste(Image.open(p).convert("RGB").resize((CELL, CELL)), (x, y))
                    except Exception:
                        pass
                lab = f"{c['src']}{j}"
                d.rectangle([x, y, x + 34, y + 18], fill=(0, 0, 0))
                d.text((x + 3, y + 2), lab, fill=(255, 255, 0), font=fb)
        board.save(BOARDS / f"rb_{m:03d}.png")
    print(f"wrote {nb} boards to {BOARDS}")


if __name__ == "__main__":
    main()

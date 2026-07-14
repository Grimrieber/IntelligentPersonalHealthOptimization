"""Requery the deferred rehunt misfires with hand-written dish-type queries.
Reuses rehunt's fetchers. Writes to rehunt_pool.json under each id's own keys
(so the SAME recorder/montage machinery works). Fast: Pixabay + Pexels + Commons.

  python tools/requery.py pool     # fetch all 3 sources for the QMAP ids
  python tools/requery.py dl
  python tools/requery.py mont
"""
import json, sys, io, os, time
from pathlib import Path
import rehunt as R  # sets UTF-8 stdout at import

# id -> corrected query (dish-type, escapes the parenthetical-gloss bug and bad literals)
QMAP = {
    8:"roasted acorn squash", 58:"apple crisp dessert", 293:"cinnamon baked apples",
    334:"coconut rice pudding", 358:"flourless chocolate cake", 371:"peach salsa bowl",
    376:"banana boat dessert chocolate", 431:"charoset", 451:"cherries jubilee dessert",
    519:"vegetarian chili bowl", 525:"date nut bars", 629:"brandade cod gratin",
    715:"cruller donut", 718:"candied orange peel", 804:"damper bread",
    1017:"croutons salad", 1115:"steamed sweet bread", 1365:"klepon rice cake",
    1411:"garlic soup bowl", 1484:"lumberjack cake", 1561:"vegan mayonnaise",
    1564:"cassava fritters", 1565:"african beef stew", 1665:"moussaka",
    1688:"naan bread", 1710:"scotch egg", 1764:"steamed bean pudding",
    1814:"pancakes stack", 1815:"vegan pancakes", 1871:"creamed peas",
    1999:"soft pretzel", 2005:"sofrito sauce", 2042:"quinoa bowl", 2106:"risotto",
    2185:"salmon cakes", 2221:"scalloped oysters casserole", 2301:"eggs hash breakfast plate",
    2505:"grilled tuna steak", 2568:"fermented sticky rice", 2623:"tiramisu",
}

def pool():
    p = R.loadpool()
    ids = sorted(QMAP)
    print(f"requery {len(ids)} ids across pixabay+pexels+commons", flush=True)
    for i, rid in enumerate(ids):
        q = QMAP[rid]; k = str(rid)
        v = p.get(k, {}); v["q"] = q; v["name"] = v.get("name", k)
        try: v["pixabay"] = R.pixabay_cands(q);
        except SystemExit: raise
        except Exception: v["pixabay"] = []
        try: v["commons"] = R.commons_cands(q)
        except Exception: v["commons"] = []
        try: v["pexels"] = R.pexels_cands(q)
        except SystemExit: raise
        except Exception: v["pexels"] = []
        p[k] = v
        time.sleep(18.5)  # paced for pexels
        if (i+1) % 5 == 0:
            R.savepool(p); print(f"{i+1}/{len(ids)} requeried", flush=True)
    R.savepool(p)
    print("done requery pool", flush=True)

if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "pool": pool()
    elif cmd == "dl": R.cmd_dl()
    elif cmd == "mont":
        # montage ONLY the requeried ids
        import rehunt
        poold = R.loadpool()
        ids = [str(i) for i in sorted(QMAP) if poold.get(str(i)) and
               (poold[str(i)].get("pixabay") or poold[str(i)].get("pexels") or poold[str(i)].get("commons"))]
        # temp: monkeypatch targets to our id list order
        from PIL import Image, ImageDraw, ImageFont
        R_ids = ids
        # reuse cmd_mont logic by writing a filtered montage here
        PB = R.PB; RH = R.RH
        try: font = ImageFont.truetype("arial.ttf", 12); fb = ImageFont.truetype("arialbd.ttf", 14)
        except Exception: font = fb = ImageFont.load_default()
        def asc(s): return s.encode("ascii","replace").decode()
        CELL, LBL, COLS, per = 190, 240, 11, 8
        n = (len(ids)+per-1)//per
        for m in range(n):
            batch = ids[m*per:(m+1)*per]
            mo = Image.new("RGB", (LBL+CELL*COLS, CELL*len(batch)), (16,16,20)); d = ImageDraw.Draw(mo)
            for r, k in enumerate(batch):
                y = r*CELL; v = poold[k]
                d.text((5, y+6), k, fill=(120,220,255), font=fb)
                d.text((6,y+26), asc(v.get("q",""))[:34], fill=(255,255,255), font=font)
                cols = [f"x{j}" for j in range(len(v.get("pixabay",[])))] + \
                       [f"p{j}" for j in range(len(v.get("pexels",[])))] + \
                       [f"c{j}" for j in range(len(v.get("commons",[])))]
                for ci, tag in enumerate(cols[:COLS]):
                    x = LBL + ci*CELL; pth = PB / f"rh_{k}_{tag}.jpg"
                    if pth.exists():
                        try: mo.paste(Image.open(pth).convert("RGB").resize((CELL,CELL)), (x, y))
                        except Exception: pass
                    col = {"c":(90,200,120),"p":(230,180,90),"x":(120,180,255)}[tag[0]]
                    d.rectangle([x, y, x+26, y+15], fill=(0,0,0))
                    d.text((x+2, y+1), tag, fill=col, font=fb)
            mo.save(RH / f"rq{m:04d}.png")
        print(f"wrote {n} requery boards rq*.png ({len(ids)} recipes)", flush=True)

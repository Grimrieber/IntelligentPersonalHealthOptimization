"""Curated second pass over the 74 flagged recipes.

The specific-name propose pass returned junk for obscure ethnic dishes
(locomotives, oboes, peanuts, coffee beans, buildings, hands at grills).
Here we FREEZE the good picks and re-query the bad ones with generic,
reliable dish-type queries that cannot return a train. No bundle writes.
"""
import json, sys, time, urllib.request
from pathlib import Path
from PIL import Image, ImageDraw
import importlib.util

HERE = Path(__file__).resolve().parent
OUT = Path("C:/Users/Stephan/AppData/Local/Temp/claude/d--Claude-Projects-IntelligentPersonalHealthOptimization/0db7466b-892c-4292-8c17-727a5e5cf47f/scratchpad")
spec = importlib.util.spec_from_file_location("vf", HERE / "vision_fix.py")
vf = importlib.util.module_from_spec(spec); spec.loader.exec_module(vf)

PROP = json.loads((HERE / "proposal_vision_refix.json").read_text(encoding="utf-8"))
NAMES = list(PROP.keys())

# index -> curated generic dish-type query. Absent index => keep existing pick.
OVERRIDE = {
 1:"spinach stew bowl", 2:"baked beans bowl", 3:"nigerian beans stew",
 4:"baked beans skillet", 5:"beans corn stew bowl", 8:"steamed sweet bread",
 9:"sesame bread ring", 11:"buffalo chicken wings", 13:"thai coconut dessert",
 14:"cheese dumplings plate", 15:"fufu swallow bowl", 16:"german pancake",
 17:"charoset date paste", 18:"pork chops tomato sauce", 19:"pumpkin beans stew",
 20:"salmon burger plate", 21:"cauliflower curry bowl", 25:"pork rib soup bowl",
 26:"lamb soup bowl", 27:"vegetable soup bowl", 28:"white bean soup bowl",
 29:"italian bean soup", 30:"nigerian fish stew", 34:"okra stew bowl",
 36:"rice hamburger egg gravy", 38:"crescent cookies sugar", 39:"whipped cream dessert",
 40:"chicken pot pie", 41:"vegetable salad plate", 42:"lemon pancake stack",
 44:"rice pudding bowl", 48:"beef brisket sandwich", 49:"mini burger sliders",
 50:"crab cakes plate", 51:"oysters on plate", 52:"roasted oysters plate",
 53:"fried cabbage potato", 54:"cooked collard greens", 55:"beef broth soup bowl",
 56:"fish noodle soup bowl", 57:"indian chaat snack", 58:"chili con carne bowl",
 59:"vegan chili bowl", 60:"lentils and rice mujadara", 61:"okra soup bowl",
 62:"african beef stew", 63:"beef stew bowl", 64:"white porridge ugali",
 65:"honey glazed donuts", 67:"asparagus sesame plate", 68:"chili curry bowl",
 70:"pasta in broth soup", 72:"beef tripe stew bowl",
}

PASS3 = {
 2:"bean chili bowl", 3:"brown bean stew", 4:"baked beans toast breakfast",
 5:"hominy corn stew", 14:"steamed dumplings plate", 15:"pounded yam soup",
 17:"date nut spread", 19:"pumpkin squash stew", 25:"spare ribs bowl",
 26:"lamb vegetable soup", 28:"tuscan white bean soup", 30:"fish tomato stew",
 34:"okra tomato stew", 40:"chicken pot pie crust", 42:"pancake stack blueberry",
 44:"rice pudding cinnamon", 48:"pulled pork sandwich plate", 50:"crab cake patties",
 51:"oysters half shell", 52:"grilled oysters shell", 56:"noodle soup bowl",
 59:"vegetarian bean chili", 60:"mujadara lentils rice cooked", 61:"green vegetable stew",
 62:"dark beef stew bowl", 64:"white maize porridge", 68:"red curry gravy bowl",
 72:"beef stew normandy",
}

PASS4 = {
 3:"lentil stew bowl", 5:"corn chowder bowl", 25:"pork rib noodle soup",
 30:"seafood stew bowl", 34:"vegetable gumbo bowl", 40:"pot pie puff pastry",
 42:"blueberry pancakes plate", 44:"rice pudding dessert bowl", 56:"asian noodle soup bowl",
 59:"bean chili pot", 61:"spinach curry bowl", 62:"beef stew brown gravy",
 68:"curry sauce bowl", 72:"beef stew red wine",
}

def _repick(mapping, tag):
    prop = json.loads((HERE / "proposal_refix2.json").read_text())
    items = list(prop.items())
    used = set(json.loads(vf.LEDGER.read_text())) if vf.LEDGER.exists() else set()
    for i, (n, info) in enumerate(items):
        ch = info.get("chosen")
        if ch and i not in mapping:
            used.add(ch[0])
    picked = set()
    for i in mapping:
        n = items[i][0]; q = mapping[i]
        chosen, alts = None, []
        for s, cid, url in vf.candidates(q):
            if cid in used or cid in picked:
                continue
            if chosen is None:
                chosen = [cid, url]
            elif len(alts) < 4:
                alts.append([cid, url])
        if chosen:
            picked.add(chosen[0])
        prop[n] = {"query": q, "chosen": chosen, "alts": alts}
        print(f"{i:2} {tag} {n[:30]:30} q={q[:24]:24} {'OK' if chosen else 'NONE'}", flush=True)
    (HERE / "proposal_refix2.json").write_text(json.dumps(prop, indent=1))
    print("updated proposal_refix2.json", flush=True)

PASS5 = {
 3:"red lentil soup bowl", 5:"corn soup bowl", 25:"ramen pork bowl",
 42:"pancakes maple syrup plate", 44:"rice pudding raisins bowl",
 59:"vegetable chili bowl", 61:"creamed spinach bowl",
}

def pass3():
    _repick(PASS3, "P3")

def pass4():
    _repick(PASS4, "P4")

def pass5():
    _repick(PASS5, "P5")

def repropose():
    used = set(json.loads(vf.LEDGER.read_text())) if vf.LEDGER.exists() else set()
    picked = set()
    out = {}
    for i, n in enumerate(NAMES):
        if i in OVERRIDE:
            q = OVERRIDE[i]
            chosen, alts = None, []
            for s, cid, url in vf.candidates(q):
                if cid in used or cid in picked:
                    continue
                if chosen is None:
                    chosen = [cid, url]
                elif len(alts) < 4:
                    alts.append([cid, url])
            if chosen:
                picked.add(chosen[0])
            out[n] = {"query": q, "chosen": chosen, "alts": alts}
            print(f"{i:2} OVERRIDE {n[:34]:34} q={q[:26]:26} {'OK' if chosen else 'NONE'}", flush=True)
        else:
            out[n] = PROP[n]  # freeze good pick
            print(f"{i:2} keep     {n[:34]:34}", flush=True)
    (HERE / "proposal_refix2.json").write_text(json.dumps(out, indent=1))
    print("wrote proposal_refix2.json", flush=True)

def montage():
    prop = json.loads((HERE / "proposal_refix2.json").read_text())
    items = list(prop.items())
    D = OUT / "rfx2"; D.mkdir(exist_ok=True)
    # fresh download every chosen url
    import concurrent.futures as cf
    def dl(i_url):
        i, url = i_url
        try:
            data = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"}), timeout=30).read()
            (D / f"{i}.jpg").write_bytes(data)
        except Exception as e:
            print("dl err", i, e)
    jobs = [(i, info["chosen"][1]) for i, (n, info) in enumerate(items) if info.get("chosen")]
    with cf.ThreadPoolExecutor(max_workers=8) as ex:
        list(ex.map(dl, jobs))
    # build chunked montages: 30 tiles, 6 cols, 240px
    cell, cols = 240, 6
    for c0 in range(0, len(items), 30):
        chunk = items[c0:c0 + 30]
        rows = (len(chunk) + cols - 1) // cols
        mo = Image.new("RGB", (cols * cell, rows * cell), (30, 20, 20)); d = ImageDraw.Draw(mo)
        for j, (n, info) in enumerate(chunk):
            i = c0 + j; x = (j % cols) * cell; y = (j // cols) * cell
            p = D / f"{i}.jpg"
            if p.exists():
                try:
                    im = Image.open(p).convert("RGB").resize((cell, cell)); mo.paste(im, (x, y))
                except Exception: pass
            d.rectangle([x, y, x + cell, y + 14], fill=(0, 0, 0))
            d.text((x + 2, y + 2), f"{i} {n[:30]}", fill=(255, 255, 0))
        f = OUT / f"rfx2_mon_{c0//30}.png"; mo.save(f); print("wrote", f, flush=True)

if __name__ == "__main__":
    {"repropose": repropose, "montage": montage, "pass3": pass3, "pass4": pass4, "pass5": pass5}[sys.argv[1]]()

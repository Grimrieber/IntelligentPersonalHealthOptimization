"""Batch replacement-image proposer for the re-audit findings.
Reads scratchpad/reaudit_r1.json (index,name,severity,problem) + audit_index.json,
auto-generates a finished-dish query per recipe, fetches+scores+picks (dedup by
Pixabay id via the ledger), writes proposal_reaudit.json + verification montages.
No bundle writes. Iterate: `pass2 <indices>` re-queries specific tiles.

Usage:
  propose <SEV>     SEV in HARD|MED|ALL — build picks for that severity
  montage           render verification montages of current proposal
  requery <file>    <file> = json {globalIndex: newquery} to override picks
"""
import json, sys, re, urllib.request, io
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import importlib.util
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
SC = Path("C:/Users/Stephan/AppData/Local/Temp/claude/d--Claude-Projects-IntelligentPersonalHealthOptimization/0db7466b-892c-4292-8c17-727a5e5cf47f/scratchpad")
spec = importlib.util.spec_from_file_location("vf", HERE / "vision_fix.py")
vf = importlib.util.module_from_spec(spec); spec.loader.exec_module(vf)

IDX = json.loads((SC / "audit_index.json").read_text(encoding="utf-8"))
FIND = json.loads((SC / "reaudit_r1.json").read_text(encoding="utf-8"))

FILLER = {"homemade","traditional","easy","quick","simple","best","authentic","classic",
          "old-fashioned","real","style","recipe","the","a","an","of","with","and","in","on",
          "II","III","IV","V","made","no-bake","baked","fried","grilled","roasted","steamed","boiled"}

def clean(name):
    main = re.sub(r"\(.*?\)", "", name).strip()
    paren = re.search(r"\((.*?)\)", name)
    if len(main.split()) <= 1 and paren:
        main = paren.group(1)
    main = re.sub(r"\b\d+[-\s]?\w*\b", "", main)      # "20-Minute"
    main = re.sub(r"[\"'&,]", " ", main)
    words = [w for w in main.split() if w.lower() not in FILLER]
    return " ".join(words).strip().lower()

def hint(name, cat):
    n = name.lower(); c = (cat or "").lower()
    if "soup" in n or "soup" in c or "chowder" in n or "bisque" in n: return "soup bowl"
    if "stew" in n or "stew" in c or "chili" in n or "curry" in n or "wot" in n or "alicha" in n: return "stew bowl"
    if "salad" in n or "salad" in c: return "salad plate"
    if "rice" in n or "rice" in c or "pilaf" in n or "biryani" in n: return "cooked rice plate"
    if "bread" in n or "loaf" in n: return "bread loaf sliced"
    if any(k in n for k in ["cake","pudding","cobbler","muffin","cookie","pie","dessert","sweet","brownie","tart","square"]): return "dessert plate"
    if "omelet" in n or "omelette" in n or "egg" in n: return "cooked plate"
    if "bean" in n or "bean" in c or "pea" in n or "lentil" in n or "chickpea" in n: return "cooked stew bowl"
    if "salsa" in n or "sauce" in n or "dip" in n or "spread" in n or "dressing" in n: return "bowl"
    if any(k in n for k in ["burger","sandwich","sliders","joe"]): return "plate"
    if "potato" in n: return "cooked dish plate"
    if "pizza" in n: return "pizza"
    return "dish plate"

def query_for(name, cat):
    base = clean(name)
    h = hint(name, cat)
    # avoid duplicating a hint word already in base
    bw = set(base.split())
    hw = " ".join(w for w in h.split() if w not in bw)
    return (base + " " + hw).strip()[:100]

def targets(sev):
    items = [x for x in FIND if sev == "ALL" or x["severity"] == sev]
    out = []
    for x in items:
        i = x["index"]; name = IDX[i]["name"]; cat = IDX[i].get("category", "")
        out.append({"index": i, "name": name, "cat": cat, "sev": x["severity"], "q": query_for(name, cat)})
    return out

def propose(sev):
    tg = targets(sev)
    used = set(json.loads(vf.LEDGER.read_text())) if vf.LEDGER.exists() else set()
    picked = set()
    prop = {}
    print(f"proposing {len(tg)} {sev} recipes", flush=True)
    for k, t in enumerate(tg):
        chosen, alts = None, []
        try:
            for s, cid, url in vf.candidates(t["q"]):
                if cid in used or cid in picked: continue
                if chosen is None: chosen = [cid, url]
                elif len(alts) < 4: alts.append([cid, url])
        except Exception as e:
            print(f"  ERR idx {t['index']}: {type(e).__name__}", flush=True)
        if chosen: picked.add(chosen[0])
        prop[str(t["index"])] = {"name": t["name"], "cat": t["cat"], "sev": t["sev"],
                                 "query": t["q"], "chosen": chosen, "alts": alts}
        if (k+1) % 25 == 0:
            print(f"  {k+1}/{len(tg)}", flush=True)
            (HERE / "proposal_reaudit.json").write_text(json.dumps(prop, indent=1, ensure_ascii=False), encoding="utf-8")
    (HERE / "proposal_reaudit.json").write_text(json.dumps(prop, indent=1, ensure_ascii=False), encoding="utf-8")
    none = sum(1 for v in prop.values() if not v["chosen"])
    print(f"wrote proposal_reaudit.json ({len(prop)} recipes, {none} NONE)", flush=True)

def requery(fpath):
    ov = json.loads(Path(fpath).read_text())          # {globalIndex(str): newquery}
    prop = json.loads((HERE / "proposal_reaudit.json").read_text())
    used = set(json.loads(vf.LEDGER.read_text())) if vf.LEDGER.exists() else set()
    for gi, info in prop.items():
        if info.get("chosen") and gi not in ov: used.add(info["chosen"][0])
    picked = set()
    for gi, q in ov.items():
        chosen, alts = None, []
        for s, cid, url in vf.candidates(q):
            if cid in used or cid in picked: continue
            if chosen is None: chosen = [cid, url]
            elif len(alts) < 4: alts.append([cid, url])
        if chosen: picked.add(chosen[0])
        prop[gi]["query"] = q; prop[gi]["chosen"] = chosen; prop[gi]["alts"] = alts
        print(f"{gi} {prop[gi]['name'][:28]:28} q={q[:26]:26} {'OK' if chosen else 'NONE'}", flush=True)
    (HERE / "proposal_reaudit.json").write_text(json.dumps(prop, indent=1, ensure_ascii=False))
    print("updated proposal_reaudit.json", flush=True)

def montage():
    prop = json.loads((HERE / "proposal_reaudit.json").read_text())
    items = list(prop.items())          # (globalIndex, info)
    D = SC / "rb"; D.mkdir(exist_ok=True)
    import concurrent.futures as cf
    def dl(job):
        gi, url = job
        try:
            data = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"}), timeout=30).read()
            (D / f"{gi}.jpg").write_bytes(data)
        except Exception as e: print("dl err", gi, e)
    jobs = [(gi, info["chosen"][1]) for gi, info in items if info.get("chosen")]
    with cf.ThreadPoolExecutor(max_workers=8) as ex: list(ex.map(dl, jobs))
    try: font = ImageFont.truetype("arial.ttf", 12)
    except Exception: font = ImageFont.load_default()
    def ascii(s): return s.encode("ascii","replace").decode()
    PER, COLS, CELL, BAR = 30, 6, 240, 24
    for c0 in range(0, len(items), PER):
        chunk = items[c0:c0+PER]; rows = (len(chunk)+COLS-1)//COLS
        mo = Image.new("RGB", (COLS*CELL, rows*CELL), (20,20,25)); d = ImageDraw.Draw(mo)
        for j, (gi, info) in enumerate(chunk):
            x = (j%COLS)*CELL; y = (j//COLS)*CELL; p = D / f"{gi}.jpg"
            if p.exists():
                try: mo.paste(Image.open(p).convert("RGB").resize((CELL,CELL)), (x,y))
                except Exception: pass
            else:
                d.rectangle([x,y,x+CELL,y+CELL], fill=(90,0,0)); d.text((x+6,y+CELL//2),"NONE",fill=(255,255,255),font=font)
            d.rectangle([x,y,x+CELL,y+BAR], fill=(0,0,0))
            d.text((x+2,y+3), f"{gi} {ascii(info['name'])[:26]}", fill=(255,255,0), font=font)
        fn = SC / f"rb_mon_{c0//PER}.png"; mo.save(fn); print("wrote", fn.name, flush=True)

if __name__ == "__main__":
    {"propose": lambda: propose(sys.argv[2]), "montage": montage,
     "requery": lambda: requery(sys.argv[2])}[sys.argv[1]]()

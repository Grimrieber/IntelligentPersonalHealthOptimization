"""Re-hunt STABLE dish photos for the needs_fix emoji-fallback recipes.

Fresh, isolated pool (rehunt_pool.json) so the verified 2573 are never touched.
Improved queries + BOTH sources (Wikimedia Commons fast; Pexels rate-limited) pooled
side by side, so board review has 3 Commons + up to 5 Pexels candidates per recipe.

Commands:
  targets                 print the needs_fix list (common-first) with the query each will use
  commons                 query Commons for every needs_fix target -> rehunt_pool.json (fast)
  pexels [start] [end]    query Pexels (paced 18.5s) common-first; merges pexels cands
  dl                      download every pooled candidate to _phaseb/rh_<id>_<src><j>.jpg
  mont [per]              build combined boards image_archive/_phaseb/rh/<NNNN>.png (8 cols)
  stats                   coverage
Apply picks with the sibling recorder: `python tools/rehunt_record.py '{id: "c0"|"p2"|null}'`
"""
import json, sys, io, re, os, time, urllib.parse, urllib.request
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
LED = ROOT / "tools" / "recipe_image_ledger.json"
POOL = ROOT / "tools" / "rehunt_pool.json"
PB = ROOT / "image_archive" / "_phaseb"
RH = PB / "rh"; RH.mkdir(parents=True, exist_ok=True)
UA = "HealthOptimizerRecipeImages/1.0 (dish photo lookup; jamesdmiller9@gmail.com)"
COMMONS = "https://commons.wikimedia.org/w/api.php"
PEXELS_API = "https://api.pexels.com/v1/search"

FILLER = {"with","and","the","of","a","an","in","on","or","style","homemade","recipe",
          "i","ii","iii","iv","classic","easy","best","traditional","authentic","real"}
COMMON_KW = ["chicken","beef","pork","soup","salad","cake","cookie","bread","pasta","rice",
    "egg","fish","pie","pancake","waffle","brownie","cheesecake","curry","chili","sandwich",
    "risotto","smoothie","stew","muffin","scone","duck","lamb","ham","turkey","fritter",
    "dumpling","pastry","apple","corn","tomato","bean","snack","dressing","potato","cabbage",
    "pudding","crumble","cobbler","tamale","gumbo","oyster","squash","zucchini","peach",
    "pineapple","chocolate","fried","roast","grilled","seared","mushroom","gazpacho","tapioca"]

def load(): return json.loads(LED.read_text(encoding="utf-8"))
def loadpool(): return json.loads(POOL.read_text(encoding="utf-8")) if POOL.exists() else {}
def savepool(p): POOL.write_text(json.dumps(p, ensure_ascii=False), encoding="utf-8")

def is_common(name):
    ln = name.lower()
    return any(k in ln for k in COMMON_KW)

def targets():
    nf = [x for x in load() if x["status"] == "needs_fix"]
    # common dishes first (fast visible wins), then obscure
    nf.sort(key=lambda x: (not is_common(x["name"]), x["id"]))
    return nf

def query_of(name):
    """Drop parenthetical, roman numerals, leading numbers, quotes. Keep dish-type words.
    For a single foreign token with an English gloss in parens, use the gloss."""
    paren = re.search(r"\((.*?)\)", name)
    main = re.sub(r"\(.*?\)", "", name).strip()
    gloss = paren.group(1).strip() if paren else ""
    base = gloss if (len(main.split()) <= 1 and gloss) else main
    base = re.sub(r"[\"'&]", " ", base)
    base = re.sub(r"\s+[IVX]+$", "", base)
    base = re.sub(r"^[\d\-\.\s]+", "", base)
    words = [w for w in base.split() if w.lower() not in FILLER]
    return " ".join(words).strip() or main or name

BAD_FILE = re.compile(r"\b(logo|icon|map|flag|coat.of.arms|diagram|chart|label|sign|"
                      r"portrait|person|people|chef|market|field|farm|packet|barcode)\b", re.I)

def commons_cands(query, want=3):
    params = {"action":"query","format":"json","generator":"search",
              "gsrsearch": query + " food dish", "gsrnamespace":"6","gsrlimit":"20",
              "prop":"imageinfo","iiprop":"url|mime","iiurlwidth":"500"}
    req = urllib.request.Request(COMMONS + "?" + urllib.parse.urlencode(params), headers={"User-Agent": UA})
    js = None
    for _ in range(4):
        try: js = json.load(urllib.request.urlopen(req, timeout=30)); break
        except Exception: time.sleep(1.0)
    if not js: return []
    pages = (js.get("query") or {}).get("pages") or {}
    rows = sorted(pages.values(), key=lambda p: p.get("index", 999))
    out = []
    for p in rows:
        ii = (p.get("imageinfo") or [{}])[0]
        mime = ii.get("mime","")
        if not mime.startswith("image/") or "svg" in mime: continue
        if BAD_FILE.search(p.get("title","")): continue
        url = ii.get("thumburl")
        if url and url.startswith("https://upload.wikimedia.org"):
            out.append((url, p.get("title","")[:40]))
        if len(out) >= want: break
    return out

PEXELS_HUMAN = ["person","people"," man","woman","women"," men","chef","hand","holding",
    "eating","kid","child"," boy"," girl","cook ","waiter","female","male","selfie","portrait","hands"]

def pexels_cands(query, want=5):
    key = os.environ.get("PEXELS_API_KEY","").strip()
    if not key: raise SystemExit("Set PEXELS_API_KEY")
    params = {"query": query + " food", "per_page":"15"}
    req = urllib.request.Request(PEXELS_API + "?" + urllib.parse.urlencode(params),
        headers={"Authorization": key, "User-Agent":"Mozilla/5.0 (compatible; IPHO-RecipeImages/1.0)"})
    js = None
    for _ in range(6):
        try: js = json.load(urllib.request.urlopen(req, timeout=30)); break
        except urllib.error.HTTPError as e:
            if e.code == 429: time.sleep(int(e.headers.get("Retry-After",0)) or 60); continue
            if e.code in (401,403): raise SystemExit("Pexels key rejected")
            return []
        except Exception: time.sleep(4)
    out = []
    for p in (js or {}).get("photos", []):
        alt = (p.get("alt") or "").lower()
        if any(h in alt for h in PEXELS_HUMAN): continue
        src = p.get("src", {})
        url = src.get("large") or src.get("medium") or src.get("original")
        if url: out.append((url, (p.get("alt") or "")[:40]))
        if len(out) >= want: break
    return out

def cmd_targets():
    for x in targets():
        tag = "C" if is_common(x["name"]) else " "
        print(f'[{tag}] {x["id"]:5}  {x["name"][:45]:45}  -> "{query_of(x["name"])}"')

def cmd_commons():
    ts = targets(); pool = loadpool(); got = 0
    for i, x in enumerate(ts):
        k = str(x["id"]); q = query_of(x["name"])
        v = pool.get(k, {"name": x["name"], "q": q})
        try: v["commons"] = commons_cands(q)
        except Exception: v["commons"] = []
        pool[k] = v
        if v["commons"]: got += 1
        time.sleep(0.3)
        if (i+1) % 30 == 0: savepool(pool); print(f"{i+1}/{len(ts)} commons; {got} w/cands", flush=True)
    savepool(pool)
    print(f"done commons: {len(ts)} targets, {got} with Commons candidates", flush=True)

def cmd_pexels(start=0, end=None):
    ts = targets(); pool = loadpool()
    todo = [x for x in ts if "pexels" not in pool.get(str(x["id"]), {})]
    if end is None: end = len(todo)
    todo = todo[start:end]
    print(f"pexels: {len(todo)} targets in slice (~{len(todo)*18.5/60:.0f} min)", flush=True)
    got = 0
    for i, x in enumerate(todo):
        k = str(x["id"]); q = query_of(x["name"])
        v = pool.get(k, {"name": x["name"], "q": q})
        try: v["pexels"] = pexels_cands(q)
        except SystemExit: raise
        except Exception: v["pexels"] = []
        pool[k] = v
        if v["pexels"]: got += 1
        time.sleep(18.5)
        if (i+1) % 10 == 0:
            savepool(pool); print(f"{start+i+1}/{start+len(todo)} pexels; {got} w/cands", flush=True)
    savepool(pool)
    print(f"done pexels slice [{start}:{end}); {got} with candidates", flush=True)

PIXABAY_API = "https://pixabay.com/api/"
PIX_BAD = re.compile(r"\b(person|people|man|woman|women|men|chef|hand|holding|eating|kid|"
                     r"child|boy|girl|cook|waiter|selfie|portrait|market|field|farm|plant|"
                     r"tree|animal|landscape|mountain|beach|building|city|logo|sign|"
                     r"raw|ingredient)\b", re.I)

def pixabay_cands(query, want=6):
    key = os.environ.get("PIXABAY_API_KEY","").strip()
    if not key: raise SystemExit("Set PIXABAY_API_KEY")
    params = {"key": key, "q": query, "image_type":"photo", "per_page":"20",
              "safesearch":"true", "category":"food", "order":"popular"}
    req = urllib.request.Request(PIXABAY_API + "?" + urllib.parse.urlencode(params),
        headers={"User-Agent":"Mozilla/5.0 (compatible; IPHO-RecipeImages/1.0)"})
    js = None
    for _ in range(5):
        try: js = json.load(urllib.request.urlopen(req, timeout=30)); break
        except urllib.error.HTTPError as e:
            if e.code == 429: time.sleep(30); continue
            return []
        except Exception: time.sleep(2)
    out = []
    for h in (js or {}).get("hits", []):
        tags = (h.get("tags") or "").lower()
        if PIX_BAD.search(tags): continue
        url = h.get("largeImageURL") or h.get("webformatURL")
        if url: out.append((url, tags[:40]))
        if len(out) >= want: break
    return out

def cmd_pixabay(start=0, end=None):
    """Pixabay is FAST (100 req/min). Pool candidates for download+local bundling."""
    ts = targets(); pool = loadpool()
    todo = [x for x in ts if "pixabay" not in pool.get(str(x["id"]), {})]
    if end is None: end = len(todo)
    todo = todo[start:end]
    print(f"pixabay: {len(todo)} targets (~{len(todo)*0.7/60:.0f} min)", flush=True)
    got = 0
    for i, x in enumerate(todo):
        k = str(x["id"]); q = query_of(x["name"])
        v = pool.get(k, {"name": x["name"], "q": q})
        try: v["pixabay"] = pixabay_cands(q)
        except SystemExit: raise
        except Exception: v["pixabay"] = []
        pool[k] = v
        if v["pixabay"]: got += 1
        time.sleep(0.6)
        if (i+1) % 30 == 0:
            savepool(pool); print(f"{start+i+1}/{start+len(todo)} pixabay; {got} w/cands", flush=True)
    savepool(pool)
    print(f"done pixabay slice [{start}:{end}); {got} with candidates", flush=True)

def alljobs(pool):
    for k, v in pool.items():
        for j, c in enumerate(v.get("commons", [])): yield k, f"c{j}", c[0]
        for j, c in enumerate(v.get("pexels", [])):  yield k, f"p{j}", c[0]
        for j, c in enumerate(v.get("pixabay", [])):  yield k, f"x{j}", c[0]

def cmd_dl():
    pool = loadpool(); ok = tot = 0
    for k, tag, url in alljobs(pool):
        tot += 1; p = PB / f"rh_{k}_{tag}.jpg"
        if p.exists() and p.stat().st_size > 500: ok += 1; continue
        for _ in range(3):
            try:
                d = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": UA}), timeout=20).read()
                if len(d) > 500: p.write_bytes(d); ok += 1; break
            except Exception: time.sleep(1.0)
        time.sleep(0.12)
    print(f"downloaded {ok}/{tot}", flush=True)

def cmd_mont(per=10):
    pool = loadpool()
    ts = targets()
    ids = [str(x["id"]) for x in ts if pool.get(str(x["id"])) and
           (pool[str(x["id"])].get("commons") or pool[str(x["id"])].get("pexels"))]
    try: font = ImageFont.truetype("arial.ttf", 12); fb = ImageFont.truetype("arialbd.ttf", 14)
    except Exception: font = fb = ImageFont.load_default()
    def asc(s): return s.encode("ascii","replace").decode()
    CELL, LBL, COLS = 190, 240, 11
    n = (len(ids)+per-1)//per
    for m in range(n):
        batch = ids[m*per:(m+1)*per]
        mo = Image.new("RGB", (LBL+CELL*COLS, CELL*len(batch)), (16,16,20)); d = ImageDraw.Draw(mo)
        for r, k in enumerate(batch):
            y = r*CELL; v = pool[k]
            d.text((5, y+6), k, fill=(120,220,255), font=fb)
            words = asc(v["name"]).split(); line=""; yy=y+26
            for w in words:
                if len(line)+len(w) > 30: d.text((6,yy),line,fill=(255,255,255),font=font); yy+=15; line=w
                else: line=(line+" "+w).strip()
            d.text((6,yy),line,fill=(255,255,255),font=font)
            d.text((6,y+CELL-16), "q: "+asc(v["q"])[:32], fill=(150,150,150), font=font)
            cols = [f"x{j}" for j in range(len(v.get("pixabay",[])))] + \
                   [f"p{j}" for j in range(len(v.get("pexels",[])))] + \
                   [f"c{j}" for j in range(len(v.get("commons",[])))]
            for ci, tag in enumerate(cols[:COLS]):
                x = LBL + ci*CELL; p = PB / f"rh_{k}_{tag}.jpg"
                if p.exists():
                    try: mo.paste(Image.open(p).convert("RGB").resize((CELL,CELL)), (x, y))
                    except Exception: pass
                col = {"c":(90,200,120), "p":(230,180,90), "x":(120,180,255)}[tag[0]]
                d.rectangle([x, y, x+26, y+15], fill=(0,0,0))
                d.text((x+2, y+1), tag, fill=col, font=fb)
        mo.save(RH / f"{m:04d}.png")
    print(f"wrote {n} boards to rh/ ({len(ids)} recipes)", flush=True)

def cmd_stats():
    pool = loadpool(); ts = targets()
    c = sum(1 for x in ts if pool.get(str(x["id"]),{}).get("commons"))
    p = sum(1 for x in ts if pool.get(str(x["id"]),{}).get("pexels"))
    any_ = sum(1 for x in ts if pool.get(str(x["id"])) and (pool[str(x["id"])].get("commons") or pool[str(x["id"])].get("pexels")))
    print(f"needs_fix targets: {len(ts)} | commons-pooled: {c} | pexels-pooled: {p} | any cands: {any_}")

if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "targets": cmd_targets()
    elif cmd == "commons": cmd_commons()
    elif cmd == "pexels": cmd_pexels(int(sys.argv[2]) if len(sys.argv)>2 else 0,
                                     int(sys.argv[3]) if len(sys.argv)>3 else None)
    elif cmd == "pixabay": cmd_pixabay(int(sys.argv[2]) if len(sys.argv)>2 else 0,
                                       int(sys.argv[3]) if len(sys.argv)>3 else None)
    elif cmd == "dl": cmd_dl()
    elif cmd == "mont": cmd_mont(int(sys.argv[2]) if len(sys.argv)>2 else 10)
    elif cmd == "stats": cmd_stats()

"""Phase B: re-home dead-link recipes to ACCURATE, STABLE dish images.

Only stable hosts are ever recorded: upload.wikimedia.org (Wikimedia Commons) and
images.pexels.com. Pixabay webformatURL is deliberately NOT used as a stored URL —
those pixabay.com/get/ links expire (that's the whole reason Phase B exists).

Durable state = tools/recipe_image_ledger.json (same ledger as Phase A). A recipe is
a Phase-B target when status in {pending, needs_fix} AND it has no verified stable url.

Commands:
  poolcommons [start] [end]  query Wikimedia Commons for each target in the id-sorted
                             slice; save up to 4 candidate stable thumb URLs/recipe to
                             phaseb_pool.json (merges, never clobbers other slices).
  download                   paced single-thread download of every pooled candidate to
                             image_archive/_phaseb/<id>_<j>.jpg (retries; skips present).
  montage [per]              row-montages (one recipe/row, 4 candidate cols) of pooled+
                             downloaded candidates into image_archive/_phaseb/mon/<NNNN>.png
  stats                      how many targets / pooled / downloaded / still-empty
"""
import json, sys, io, re, time, urllib.parse, urllib.request
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
LED = ROOT / "tools" / "recipe_image_ledger.json"
POOL = ROOT / "tools" / "phaseb_pool.json"
PB = ROOT / "image_archive" / "_phaseb"; PB.mkdir(parents=True, exist_ok=True)
MON = PB / "mon"; MON.mkdir(exist_ok=True)
UA = "HealthOptimizerRecipeImages/1.0 (dish photo lookup; jamesdmiller9@gmail.com)"
COMMONS = "https://commons.wikimedia.org/w/api.php"

FILLER = {"with","and","the","of","a","an","in","on","or","style","homemade","recipe","i","ii","iii"}

def load(): return json.loads(LED.read_text(encoding="utf-8"))
def loadpool(): return json.loads(POOL.read_text(encoding="utf-8")) if POOL.exists() else {}
def savepool(p): POOL.write_text(json.dumps(p, ensure_ascii=False), encoding="utf-8")

def targets():
    led = load()
    t = [x for x in led if x["status"] in ("pending","needs_fix")]
    return sorted(t, key=lambda x: x["id"])

def query_of(name):
    """Prefer the native dish name; fall back to English parenthetical only when the
    main name is a single foreign token. Keep place/descriptive words."""
    paren = re.search(r"\((.*?)\)", name)
    main = re.sub(r"\(.*?\)", "", name).strip()
    desc = paren.group(1).strip() if paren else ""
    base = desc if (len(main.split()) <= 1 and desc) else main
    base = re.sub(r"[\"'&]", " ", base)
    base = re.sub(r"\s+[IVX]+$", "", base)
    base = re.sub(r"^[\d\-\.\s]+", "", base)
    words = [w for w in base.split() if w.lower() not in FILLER]
    return " ".join(words).strip() or name

BAD_FILE = re.compile(r"\b(logo|icon|map|flag|coat.of.arms|diagram|chart|label|sign|"
                      r"portrait|person|people|chef|cook|market|field|farm|plant|"
                      r"raw|ingredient|packet|package|bottle|can|tin|box)\b", re.I)

def commons_cands(query, want=4):
    """Return [(thumburl, filetitle)] finished-dish-ish stable Commons thumbs."""
    params = {"action":"query","format":"json","generator":"search",
              "gsrsearch": query + " food", "gsrnamespace":"6","gsrlimit":"20",
              "prop":"imageinfo","iiprop":"url|mime","iiurlwidth":"500"}
    req = urllib.request.Request(COMMONS + "?" + urllib.parse.urlencode(params), headers={"User-Agent": UA})
    js = None
    for _ in range(4):
        try:
            js = json.load(urllib.request.urlopen(req, timeout=30)); break
        except Exception: time.sleep(1.0)
    if not js: return []
    pages = (js.get("query") or {}).get("pages") or {}
    rows = sorted(pages.values(), key=lambda p: p.get("index", 999))
    out = []
    for p in rows:
        ii = (p.get("imageinfo") or [{}])[0]
        mime = ii.get("mime","")
        if not mime.startswith("image/") or "svg" in mime: continue
        title = p.get("title","")
        if BAD_FILE.search(title): continue
        url = ii.get("thumburl")
        if url and url.startswith("https://upload.wikimedia.org"):
            out.append((url, title))
        if len(out) >= want: break
    return out

def poolcommons(start=0, end=None):
    ts = targets()
    if end is None: end = len(ts)
    ts = ts[start:end]
    pool = loadpool()
    for i, x in enumerate(ts):
        key = str(x["id"])
        if pool.get(key): continue          # already pooled in a prior run
        q = query_of(x["name"])
        try: cands = commons_cands(q)
        except Exception: cands = []
        pool[key] = {"name": x["name"], "q": q, "cands": cands}
        time.sleep(0.35)
        if (i+1) % 25 == 0:
            savepool(pool)
            got = sum(1 for v in pool.values() if v.get("cands"))
            print(f"{start+i+1}/{start+len(ts)} pooled; {got} with candidates", flush=True)
    savepool(pool)
    got = sum(1 for v in pool.values() if v.get("cands"))
    print(f"done slice [{start}:{end}); pool now {len(pool)} recipes, {got} with candidates", flush=True)

PEXELS_API = "https://api.pexels.com/v1/search"
PEXELS_HUMAN = ["person","people"," man","woman","women"," men","chef","hand","holding",
                "eating","kid","child"," boy"," girl","cook ","waiter","female","male",
                "selfie","portrait","hands"]

def pexels_cands(query, want=3):
    key = __import__("os").environ.get("PEXELS_API_KEY","").strip()
    if not key: raise SystemExit("Set PEXELS_API_KEY")
    params = {"query": query, "per_page":"10", "orientation":"square"}
    req = urllib.request.Request(PEXELS_API + "?" + urllib.parse.urlencode(params),
                                 headers={"Authorization": key,
                                          "User-Agent":"Mozilla/5.0 (compatible; IPHO-RecipeImages/1.0)"})
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

def poolpexels(start=0, end=None):
    """For targets that Commons left with NO candidates, query Pexels (stable urls).
    Paced ~18.5s to stay under 200 req/hr. Merges into the same pool (cands list)."""
    pool = loadpool()
    # Every non-verified target (pending OR needs_fix whose Commons cands were rejected)
    # gets fresh Pexels candidates. Skip ones already Pexels-pooled so this is resumable.
    miss = [x for x in targets() if pool.get(str(x["id"]),{}).get("src") != "pexels"]
    miss = sorted(miss, key=lambda x: x["id"])
    if end is None: end = len(miss)
    miss = miss[start:end]
    print(f"pexels: {len(miss)} Commons-miss targets in slice (~{len(miss)*18.5/3600:.1f} h)", flush=True)
    for i, x in enumerate(miss):
        key = str(x["id"]); q = query_of(x["name"])
        try: cands = pexels_cands(q)
        except SystemExit: raise
        except Exception: cands = []
        prev = pool.get(key, {"name": x["name"], "q": q})
        prev["cands"] = cands; prev["src"] = "pexels"; pool[key] = prev
        time.sleep(18.5)
        if (i+1) % 10 == 0:
            savepool(pool)
            got = sum(1 for m in miss[:i+1] if pool.get(str(m["id"]),{}).get("cands"))
            print(f"{start+i+1}/{start+len(miss)} pexels-pooled; {got} got candidates", flush=True)
    savepool(pool)
    print(f"done pexels slice [{start}:{end})", flush=True)

def download():
    pool = loadpool()
    jobs = [(k, j, c[0]) for k, v in pool.items() for j, c in enumerate(v.get("cands", []))]
    ok = 0; streak = 0
    for i, (k, j, url) in enumerate(jobs):
        p = PB / f"{k}_{j}.jpg"
        if p.exists() and p.stat().st_size > 500: ok += 1; continue
        got = False
        for _ in range(2):
            try:
                d = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": UA}), timeout=15).read()
                if len(d) > 500: p.write_bytes(d); got = True; break
            except urllib.error.HTTPError as e:
                if e.code in (429, 503):
                    wait = min(int(e.headers.get("Retry-After", 0) or 20), 30)
                    streak += 1; time.sleep(wait * (1 + streak // 5)); continue   # back off harder on a run
                break
            except Exception:
                time.sleep(1.0)
        if got: ok += 1; streak = 0
        time.sleep(0.2)
        if (i+1) % 100 == 0: print(f"{i+1}/{len(jobs)} ({ok} ok, streak={streak})", flush=True)
    print(f"downloaded {ok}/{len(jobs)} candidates", flush=True)

def montage(per=10):
    pool = loadpool()
    keys = [k for k in sorted(pool, key=lambda z: int(z)) if pool[k].get("cands")
            and any((PB / f"{k}_{j}.jpg").exists() for j in range(len(pool[k]["cands"])))]
    try: font = ImageFont.truetype("arial.ttf", 12); fb = ImageFont.truetype("arialbd.ttf", 14)
    except Exception: font = fb = ImageFont.load_default()
    def asc(s): return s.encode("ascii","replace").decode()
    CELL, LBL, COLS = 235, 250, 4
    n = (len(keys)+per-1)//per
    for m in range(n):
        batch = keys[m*per:(m+1)*per]
        mo = Image.new("RGB", (LBL+CELL*COLS, CELL*len(batch)), (16,16,20)); d = ImageDraw.Draw(mo)
        for r, k in enumerate(batch):
            y = r*CELL; v = pool[k]
            d.text((5, y+6), k, fill=(120,220,255), font=fb)
            words = asc(v["name"]).split(); line=""; yy=y+26
            for w in words:
                if len(line)+len(w) > 28: d.text((6,yy),line,fill=(255,255,255),font=font); yy+=16; line=w
                else: line=(line+" "+w).strip()
            d.text((6,yy),line,fill=(255,255,255),font=font)
            d.text((6,y+CELL-18), "q: "+asc(v["q"])[:30], fill=(150,150,150), font=font)
            for j in range(len(v["cands"])):
                x = LBL + j*CELL; p = PB / f"{k}_{j}.jpg"
                if p.exists():
                    try: mo.paste(Image.open(p).convert("RGB").resize((CELL,CELL)), (x, y))
                    except Exception: pass
                d.rectangle([x, y, x+20, y+16], fill=(0,0,0))
                d.text((x+3, y+1), str(j), fill=(255,255,0), font=fb)
        mo.save(MON / f"{m:04d}.png")
    print(f"wrote {n} montages to image_archive/_phaseb/mon/ (per={per}, {len(keys)} recipes)", flush=True)

def record(picks):
    """picks: dict {id(str/int): j|None}. j = chosen candidate column -> set that
    recipe's ledger url to the stable Commons thumb, status=verified_ok. None -> leave
    as needs_fix with a note that Commons had no accurate candidate (Pexels/Pixabay dig
    pile). Idempotent; only touches ids present in picks."""
    led = load(); pool = loadpool()
    by = {x["id"]: x for x in led}
    ok = miss = 0
    for k, j in picks.items():
        rid = int(k); x = by.get(rid)
        if not x: continue
        v = pool.get(str(rid), {})
        if j is None or not v.get("cands") or j >= len(v["cands"]):
            x["status"] = "needs_fix"; x["checked"] = True
            x["note"] = (x.get("note") or "") + "[commons: no accurate candidate] "
            miss += 1; continue
        url, title = v["cands"][j]
        host = urllib.parse.urlparse(url).netloc
        x["url"] = url; x["host"] = host
        x["stability"] = "wikimedia" if "wikimedia" in host else ("pexels" if "pexels" in host else "other")
        x["status"] = "verified_ok"; x["checked"] = True; x["downloaded"] = True
        x["archive_file"] = f"_phaseb/{rid}_{j}.jpg"
        x["note"] = (x.get("note") or "") + f"[phaseb {x['stability']}: {title}] "
        ok += 1
    LED.write_text(json.dumps(led, ensure_ascii=False, indent=0), encoding="utf-8")
    from collections import Counter
    print(f"recorded {ok} verified, {miss} still-needs; status={dict(Counter(x['status'] for x in led))}", flush=True)

def stats():
    ts = targets(); pool = loadpool()
    pooled = sum(1 for x in ts if pool.get(str(x["id"])))
    withc = sum(1 for x in ts if pool.get(str(x["id"]),{}).get("cands"))
    dl = len(list(PB.glob("*.jpg")))
    print(f"targets={len(ts)} pooled={pooled} with_candidates={withc} downloaded_files={dl}")

if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "stats"
    if cmd == "poolcommons":
        s = int(sys.argv[2]) if len(sys.argv) > 2 else 0
        e = int(sys.argv[3]) if len(sys.argv) > 3 else None
        poolcommons(s, e)
    elif cmd == "poolpexels":
        s = int(sys.argv[2]) if len(sys.argv) > 2 else 0
        e = int(sys.argv[3]) if len(sys.argv) > 3 else None
        poolpexels(s, e)
    elif cmd == "download": download()
    elif cmd == "montage": montage(int(sys.argv[2]) if len(sys.argv) > 2 else 10)
    elif cmd == "record": record(json.loads(sys.argv[2]))
    else: stats()

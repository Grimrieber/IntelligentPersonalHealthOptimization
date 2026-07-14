"""
Systematic, one-family-at-a-time image fixer with human (my) vision in the loop.

Root problem this fixes: query_for() over-strips names to a generic head noun, so
"Mexican Rice"/"Spanish Rice"/"Egyptian Rice with Vermicelli" all became "rice" and
pulled sushi/ramen/meat from one generic pool. Here we query each recipe by its
SPECIFIC full name, keep several finished-dish candidates, and dedup GLOBALLY by
Pixabay image id via a persistent ledger (used_ids.json) so no duplicate can ever
be reintroduced across batches.

Usage:
  propose <namesfile>  : for each recipe name (one per line), fetch specific-query
                         candidates, pick top finished-dish (id not already used),
                         write proposal_<tag>.json + a montage for me to eyeball.
  apply   <proposalfile>: patch the bundle from an (optionally hand-edited) proposal
                         and record every assigned id in the ledger.

Reads PIXABAY_API_KEY from env. Never writes the bundle in 'propose' mode.
"""
import gzip, json, os, re, sys, time, urllib.parse, urllib.request
from pathlib import Path
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
LEDGER = HERE / "used_ids.json"
OUT = Path("C:/Users/Stephan/AppData/Local/Temp/claude/d--Claude-Projects-IntelligentPersonalHealthOptimization/0db7466b-892c-4292-8c17-727a5e5cf47f/scratchpad")
API = "https://pixabay.com/api/"
KEY = os.environ.get("PIXABAY_API_KEY", "").strip()

import importlib.util
_spec = importlib.util.spec_from_file_location("fa", HERE / "pixabay_finished_audit.py")
_fa = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(_fa)
score = _fa.score            # reuse the finished-dish scorer (reject raw/scenery/human, prefer plated)

FILLER = {"with", "and", "the", "of", "a", "an", "in", "on", "or", "style", "homemade", "recipe"}

def specific_query(name):
    """Full dish name, cleaned but NOT reduced to a head noun. Keep place + descriptive
    words (mexican, spanish, vermicelli). Prefer the native/main name; fall back to the
    English parenthetical only when the main name is a single foreign token."""
    paren = re.search(r"\((.*?)\)", name)
    main = re.sub(r"\(.*?\)", "", name).strip()
    desc = paren.group(1).strip() if paren else ""
    base = main
    if len(main.split()) <= 1 and desc:          # e.g. "Chibwabwa (Zambian Pumpkin Leaves)"
        base = desc
    base = re.sub(r"[\"'&]", " ", base)
    base = re.sub(r"\s+[IVX]+$", "", base)         # trailing roman numeral (Mexican Rice II)
    base = re.sub(r"^[\d\-\.\s]+", "", base)
    words = [w for w in base.split() if w.lower() not in FILLER]
    return " ".join(words).strip().lower() or name.lower()

def call(params):
    req = urllib.request.Request(API + "?" + urllib.parse.urlencode(params),
                                 headers={"User-Agent": "Mozilla/5.0"})
    for _ in range(5):
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                return json.load(r)
        except urllib.error.HTTPError as e:
            if e.code == 429:
                time.sleep(int(e.headers.get("Retry-After", 0)) or 30); continue
            if e.code in (400, 401): raise SystemExit(f"key rejected ({e.code})")
            return None
        except Exception:
            time.sleep(3); continue
    return None

def candidates(query, pace=0.6):
    """Return [(score, id, url), …] finished-dish first, for a SPECIFIC query."""
    base = {"key": KEY, "image_type": "photo", "per_page": "60", "safesearch": "true"}
    js = call(dict(base, q=query[:100], category="food"))
    if not js or js.get("totalHits", 0) < 3:
        js = call(dict(base, q=query[:100])) or js
    time.sleep(pace)
    out = []
    for h in (js or {}).get("hits", []):
        tags = [t.strip() for t in (h.get("tags") or "").lower().split(",") if t.strip()]
        s = score(tags, query)
        if s is not None and h.get("id") and h.get("webformatURL"):
            out.append((s, h["id"], h["webformatURL"]))
    out.sort(key=lambda x: -x[0])
    return out

def propose(namesfile):
    names = [l.strip() for l in Path(namesfile).read_text(encoding="utf-8").splitlines() if l.strip()]
    tag = Path(namesfile).stem
    used = set(json.loads(LEDGER.read_text())) if LEDGER.exists() else set()
    proposal = {}
    for n in names:
        q = specific_query(n)
        cands = candidates(q)
        chosen = None; alts = []
        for s, cid, url in cands:
            if cid in used or cid in [a[0] for a in alts] or (chosen and cid == chosen[0]):
                continue
            if chosen is None:
                chosen = (cid, url)
            elif len(alts) < 4:
                alts.append((cid, url))
        proposal[n] = {"query": q, "chosen": chosen, "alts": alts}
        if chosen:
            used.add(chosen[0])
        print(f"  {n[:40]:40} q={q[:28]:28} {'OK' if chosen else 'NONE'}")
    Path(HERE / f"proposal_{tag}.json").write_text(json.dumps(proposal, indent=1))
    _montage(proposal, f"propose_{tag}.png")
    print(f"wrote proposal_{tag}.json and propose_{tag}.png")

def _montage(proposal, fname):
    items = list(proposal.items())
    cols = 4; rows = (len(items) + cols - 1) // cols; cell = 250
    mo = Image.new("RGB", (cols * cell, rows * cell), (20, 20, 20)); d = ImageDraw.Draw(mo)
    for i, (n, info) in enumerate(items):
        ch = info.get("chosen")
        if not ch:
            continue
        try:
            data = urllib.request.urlopen(urllib.request.Request(ch[1], headers={"User-Agent": "Mozilla/5.0"}), timeout=30).read()
            p = OUT / f"vf{i}.jpg"; p.write_bytes(data)
            im = Image.open(p).convert("RGB").resize((cell, cell)); x = (i % cols) * cell; y = (i // cols) * cell
            mo.paste(im, (x, y)); d.rectangle([x, y, x + cell, y + 28], fill=(0, 0, 0))
            d.text((x + 2, y + 2), f"{i}:{n[:26]}", fill=(255, 255, 0))
        except Exception as e:
            print("montage err", n, e)
    mo.save(OUT / fname)

def apply(proposalfile):
    proposal = json.loads(Path(proposalfile).read_text())
    used = set(json.loads(LEDGER.read_text())) if LEDGER.exists() else set()
    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    by = {r["name"]: r for r in b["recipes"]}
    n = 0
    for name, info in proposal.items():
        ch = info.get("chosen")
        if ch and name in by:
            by[name]["imageUrl"] = ch[1]; used.add(ch[0]); n += 1
    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)
    LEDGER.write_text(json.dumps(sorted(used)))
    print(f"applied {n} images; ledger now {len(used)} ids; schemaVersion={b['schemaVersion']}")

def singletons(_arg):
    """Auto-fix every SINGLETON recipe (unique dish name, non-Wikimedia) with a
    specific-name query. Safe to automate: no intra-family duplicate risk. Dedups
    by id against the global ledger. Device spot-check afterwards."""
    _q = _fa.query_for
    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    from collections import Counter
    from urllib.parse import urlparse
    fam_size = Counter(_q(r["name"]) for r in recipes)
    def is_wiki(r):
        u = r.get("imageUrl"); return u and urlparse(u).netloc == "upload.wikimedia.org"
    only_singletons = _arg == "singletons"     # else 'all' → every non-wiki recipe
    targets = [r for r in recipes if not is_wiki(r)
               and (fam_size[_q(r["name"])] == 1 or not only_singletons)]
    used = set(json.loads(LEDGER.read_text())) if LEDGER.exists() else set()
    print(f"singletons: {len(targets)} recipes (~{len(targets)*0.6/60:.0f} min)", flush=True)
    done = 0
    for r in targets:
        q = specific_query(r["name"])
        pick = next(((cid, url) for s, cid, url in candidates(q) if cid not in used), None)
        if pick:
            r["imageUrl"] = pick[1]; used.add(pick[0]); done += 1
        if done and done % 50 == 0:
            LEDGER.write_text(json.dumps(sorted(used)))
            with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
                json.dump(b, f, ensure_ascii=False)
            print(f"  {done}/{len(targets)}", flush=True)
    LEDGER.write_text(json.dumps(sorted(used)))
    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)
    print(f"done: fixed {done}/{len(targets)}; ledger {len(used)} ids; schemaVersion={b['schemaVersion']}", flush=True)

if __name__ == "__main__":
    if not KEY and sys.argv[1] in ("propose", "singletons", "autoall"):
        raise SystemExit("Set PIXABAY_API_KEY first.")
    cmd = sys.argv[1]
    {"propose": propose, "apply": apply,
     "singletons": lambda a: singletons("singletons"),
     "autoall": lambda a: singletons("all")}[cmd](sys.argv[-1])

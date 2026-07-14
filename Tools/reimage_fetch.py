"""
Re-image pipeline, step 1: fetch candidate photos for the 631 recipes that were
sharing a generic stock photo with a different dish (tools/reimage_targets.json).

Pulls candidates from THREE sources so obscure dishes still get options:
  * Pixabay  (category=food, tag-based scenery/people/animal rejection)
  * Pexels   (CDN-stable; alt-text scenery/people rejection)
  * Wikimedia Commons (native-name search; great for regional/traditional dishes)

Downloads every candidate to scratchpad/reimg_cand/<id>_<src><j>.jpg and records
url+host in scratchpad/reimg_pool.json so the montage step (reimage_montage.py) and
apply step (reimage_apply.py) can build boards and localize the chosen picks.

Keys are read from env ONLY (PIXABAY_API_KEY, PEXELS_API_KEY) — never committed.

Usage:  python tools/reimage_fetch.py            # fetch all targets (resumable)
        python tools/reimage_fetch.py 0 200      # fetch a slice [start,end)
"""
import json, io, os, re, sys, time, urllib.parse, urllib.request, urllib.error
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor

HERE = Path(__file__).resolve().parent
SC = Path("C:/Users/Stephan/AppData/Local/Temp/claude/"
          "d--Claude-Projects-IntelligentPersonalHealthOptimization/"
          "0db7466b-892c-4292-8c17-727a5e5cf47f/scratchpad")
TARGETS = HERE / "reimage_targets.json"
CAND = SC / "reimg_cand"; CAND.mkdir(parents=True, exist_ok=True)
POOL = SC / "reimg_pool.json"
PIXKEY = os.environ.get("PIXABAY_API_KEY", "").strip()
PEXKEY = os.environ.get("PEXELS_API_KEY", "").strip()
UA = "Mozilla/5.0 (IntelligentPersonalHealthOptimization; personal health app)"

PLACE = set("""austrian bavarian german french italian spanish greek turkish russian polish hungarian
swedish norwegian danish dutch belgian swiss portuguese english irish scottish welsh british american
mexican brazilian argentine peruvian cuban jamaican moroccan egyptian ethiopian nigerian kenyan indian
pakistani thai vietnamese chinese japanese korean filipino indonesian malaysian lebanese syrian persian
iranian iraqi afghan australian canadian hawaiian cajun creole sicilian tuscan provencal catalan basque
georgian armenian ukrainian czech slovak croatian serbian romanian bulgarian finnish icelandic tibetan
nepalese bangladeshi cambodian burmese mongolian zambian rwandan ugandan gambian ghanaian somali
liberian cameroonian sindhi senegalese libyan togolese""".split())
FILLER = {"with", "and", "the", "of", "a", "an", "in", "on", "or", "style", "homemade", "recipe", "traditional"}
REJECT = set("""landscape mountain lake river valley village town city skyline street building cathedral church
castle bridge canal monument panorama aerial forest beach island coast sky sunset nature architecture
animal wildlife bird cow cattle bull buffalo bison horse sheep goat deer dog cat pet zoo person people
man woman women men child kid boy girl portrait selfie crowd flag map logo car vehicle boat ship
hand hands finger fingers""".split())


def qbase(name):
    """Specific full-name query; prefer English paren description for foreign names."""
    paren = re.search(r"\((.*?)\)", name)
    main = re.sub(r"\(.*?\)", "", name).strip()
    desc = paren.group(1).strip() if paren else ""
    base = main
    if len(main.split()) <= 1 and desc:
        base = desc
    base = re.sub(r"[\"'&]", " ", base)
    base = re.sub(r"\s+[IVX]+$", "", base)
    base = re.sub(r"^[\d\-\.\s]+", "", base)
    words = [w for w in base.split() if w.lower() not in FILLER]
    return " ".join(words).strip() or name


def http(url, headers=None, tries=4):
    for a in range(tries):
        try:
            req = urllib.request.Request(url, headers=headers or {"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=30) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            if e.code == 429:
                time.sleep(int(e.headers.get("Retry-After", 0)) or 20); continue
            return None
        except Exception:
            time.sleep(2); continue
    return None


def pixabay(q, n=4):
    if not PIXKEY:
        return []
    base = {"key": PIXKEY, "image_type": "photo", "per_page": "40", "safesearch": "true", "q": q[:100]}
    data = http("https://pixabay.com/api/?" + urllib.parse.urlencode(dict(base, category="food")))
    js = json.loads(data) if data else None
    if not js or js.get("totalHits", 0) < 2:
        data = http("https://pixabay.com/api/?" + urllib.parse.urlencode(base))
        js = json.loads(data) if data else js
    out = []
    for h in (js or {}).get("hits", []):
        tags = [t.strip() for t in (h.get("tags") or "").lower().split(",")]
        if any(t in REJECT for t in tags):
            continue
        if h.get("largeImageURL"):
            out.append(h["largeImageURL"])
        if len(out) >= n:
            break
    return out


def pexels(q, n=4):
    if not PEXKEY:
        return []
    data = http("https://api.pexels.com/v1/search?" + urllib.parse.urlencode(
        {"query": q[:100], "per_page": "30"}), headers={"Authorization": PEXKEY, "User-Agent": UA})
    js = json.loads(data) if data else None
    out = []
    for p in (js or {}).get("photos", []):
        alt = (p.get("alt") or "").lower()
        if any(w in alt.split() for w in REJECT):
            continue
        src = p.get("src", {})
        u = src.get("large") or src.get("medium")
        if u:
            out.append(u)
        if len(out) >= n:
            break
    return out


def _commons_search(search, n):
    api = ("https://commons.wikimedia.org/w/api.php?action=query&generator=search"
           "&gsrnamespace=6&gsrlimit=30&prop=imageinfo&iiprop=url|extmetadata"
           "&iiurlwidth=500&format=json&gsrsearch=" + urllib.parse.quote(search[:100]))
    data = http(api)
    js = json.loads(data) if data else None
    out = []
    pages = ((js or {}).get("query", {}) or {}).get("pages", {})
    for p in pages.values():
        ii = (p.get("imageinfo") or [{}])[0]
        title = (p.get("title") or "").lower()
        if any(bad in title for bad in ("map", "flag", "logo", "portrait", "person", "icon", "svg")):
            continue
        u = ii.get("thumburl")
        if u and u.lower().rsplit(".", 1)[-1] in ("jpg", "jpeg", "png"):
            out.append(u)
        if len(out) >= n:
            break
    return out


def commons(q, n=4):
    # narrowed food-biased search first; fall back to bare query for native-name dishes
    out = _commons_search(q + " food dish", n)
    if len(out) < 2:
        seen = set(out)
        for u in _commons_search(q, n):
            if u not in seen:
                out.append(u); seen.add(u)
            if len(out) >= n:
                break
    return out


def main():
    targets = json.load(io.open(TARGETS, encoding="utf-8"))
    a = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    z = int(sys.argv[2]) if len(sys.argv) > 2 else len(targets)
    targets = targets[a:z]
    pool = json.load(io.open(POOL, encoding="utf-8")) if POOL.exists() else {}
    print(f"fetching candidates for {len(targets)} targets [{a}:{z}]  pix={bool(PIXKEY)} pex={bool(PEXKEY)}")
    for k, t in enumerate(targets):
        rid = str(t["id"])
        if rid in pool and pool[rid].get("cands"):
            continue
        q = t.get("q") or qbase(t["name"])
        cands = []
        for src, fn in (("px", pixabay), ("pe", pexels), ("wm", commons)):
            try:
                for u in fn(q):
                    cands.append({"src": src, "url": u})
            except Exception as e:
                print(f"  ERR {rid} {src} {type(e).__name__}")
        pool[rid] = {"name": t["name"], "query": q, "cands": cands}
        time.sleep(0.5)
        if (k + 1) % 20 == 0:
            json.dump(pool, io.open(POOL, "w", encoding="utf-8"), ensure_ascii=False)
            print(f"  {k+1}/{len(targets)}  (pool {len(pool)})", flush=True)
    json.dump(pool, io.open(POOL, "w", encoding="utf-8"), ensure_ascii=False)

    # download all candidate images
    jobs = []
    for rid, info in pool.items():
        for j, c in enumerate(info["cands"]):
            jobs.append((rid, j, c["src"], c["url"]))
    def dl(job):
        rid, j, src, url = job
        p = CAND / f"{rid}_{src}{j}.jpg"
        if p.exists() and p.stat().st_size > 800:
            return
        data = http(url)
        if data and len(data) > 800:
            p.write_bytes(data)
    with ThreadPoolExecutor(max_workers=8) as ex:
        list(ex.map(dl, jobs))
    print(f"DONE: pool {len(pool)} targets, {sum(len(v['cands']) for v in pool.values())} candidates downloaded")


if __name__ == "__main__":
    main()

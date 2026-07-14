"""
Post-process the shipped recipe bundle to add a Wikimedia Commons image URL to
each recipe that has one. Recipes come from Wikibooks Cookbook (CC-BY-SA); their
lead photos live on Commons. We store the THUMBNAIL URL (~500px) so the app can
load images at runtime without bloating the APK. Recipes with no photo get no
imageUrl and fall back to the category emoji tile in the UI.

DB-free: reads/writes Resources/Raw/wikibooks_bundle.json.gz directly.
"""
import gzip, json, time, urllib.parse, urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
API = "https://en.wikibooks.org/w/api.php"
UA = "IPHO-RecipeImages/1.0 (personal health app; recipe images from Wikibooks Cookbook)"

BAD = ["logo", "commons-", "wikipedia", "wikivoyage", "wiktionary", "wikibooks",
       "icon", "symbol", "flag_of", "nuvola", "edit-", "ambox", "question_book",
       "wiki_letter", "crystal_", "gnome-",
       # no humans / non-dish subjects
       "chef", "person", "people", "portrait", "_man_", "_woman", "_boy", "_girl",
       "holding", "_hand", "hands_", "eating", "waiter", "cooking_", "-cooking",
       # non-food places / objects that leak in
       "canal", "river", "_lake", "building", "street", "market", "storefront",
       "shopfront", "_map", "factory", "machine", "cooker", "crock", "appliance"]

def is_photo(title: str) -> bool:
    tl = title.lower()
    if not (tl.endswith(".jpg") or tl.endswith(".jpeg") or tl.endswith(".png")):
        return False
    return not any(b in tl for b in BAD)

def page_title(url: str) -> str:
    return urllib.parse.unquote(url.split("/wiki/", 1)[1]).replace("_", " ") if "/wiki/" in url else ""

def api_get(params):
    params = dict(params, maxlag="5")
    for attempt in range(6):
        req = urllib.request.Request(API + "?" + urllib.parse.urlencode(params), headers={"User-Agent": UA})
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                return json.load(r)
        except urllib.error.HTTPError as e:
            if e.code == 429:
                wait = int(e.headers.get("Retry-After", 0)) or (5 * (attempt + 1))
                print(f"    429 — backing off {wait}s")
                time.sleep(wait)
                continue
            raise
    raise RuntimeError("giving up after repeated 429s")

def chunks(seq, n):
    for i in range(0, len(seq), n):
        yield seq[i:i + n]

def main():
    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    # map normalized page title -> recipe
    by_title = {}
    for r in recipes:
        t = page_title(str(r.get("source", "")))
        if t:
            by_title[t.lower()] = r
    titles = list({page_title(str(r.get("source", ""))) for r in recipes if page_title(str(r.get("source", "")))})
    print(f"{len(recipes)} recipes, {len(titles)} unique pages")

    # Pass 1: prop=images -> pick a candidate File: per page
    candidate = {}  # page-title-lower -> File:xxx
    done = 0
    for chunk in chunks(titles, 40):
        try:
            js = api_get({"action": "query", "prop": "images", "imlimit": "100",
                          "format": "json", "titles": "|".join(chunk)})
        except Exception as e:
            print("images err:", e); continue
        # normalization map (requested -> normalized)
        norm = {n["from"]: n["to"] for n in js.get("query", {}).get("normalized", [])}
        for _, p in js.get("query", {}).get("pages", {}).items():
            title = p.get("title", "")
            photos = [im["title"] for im in p.get("images", []) if is_photo(im["title"])]
            if photos:
                candidate[title.lower()] = photos[0]
        done += len(chunk)
        if done % 400 == 0:
            print(f"  images: {done}/{len(titles)} pages, {len(candidate)} with photo")
        time.sleep(1.1)
    print(f"pages with a candidate photo: {len(candidate)}")

    # Pass 2: imageinfo -> thumbnail URL for each unique File:
    files = list(set(candidate.values()))
    thumb = {}  # File:xxx -> thumburl
    for chunk in chunks(files, 50):
        try:
            js = api_get({"action": "query", "prop": "imageinfo", "iiprop": "url",
                          "iiurlwidth": "500", "format": "json", "titles": "|".join(chunk)})
        except Exception as e:
            print("imageinfo err:", e); continue
        for _, p in js.get("query", {}).get("pages", {}).items():
            ii = p.get("imageinfo", [])
            if ii:
                thumb[p.get("title", "")] = ii[0].get("thumburl") or ii[0].get("url")
        time.sleep(1.1)
    print(f"resolved thumbnail urls: {len(thumb)}")

    # Attach imageUrl to recipes
    attached = 0
    for tl, fileTitle in candidate.items():
        r = by_title.get(tl)
        url = thumb.get(fileTitle)
        if r is not None and url:
            r["imageUrl"] = url
            attached += 1
    print(f"attached imageUrl to {attached}/{len(recipes)} recipes ({100*attached/len(recipes):.1f}%)")

    if attached == 0:
        print("no images attached — NOT writing bundle")
        return

    # Bump schema so the app re-imports the bundle
    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)
    print(f"wrote {BUNDLE} (schemaVersion={b['schemaVersion']})")

if __name__ == "__main__":
    main()

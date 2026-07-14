"""Permanent recipe-image ARCHIVE + VERIFICATION tool (reference copies only — NOT the
images shipped in the app; the app uses the ledger's `url`).

Durable across sessions:
  - tools/recipe_image_ledger.json  : per-recipe {id,name,category,url,host,stability,
                                        status,checked,note,archive_file,downloaded}
  - image_archive/                  : {id:04d}_{slug}.jpg reference copies (gitignored)

Commands:
  archive [stability]   download current `url` for entries (default: stable) into archive,
                        paced+retried, and record archive_file/downloaded in the ledger
  montage [stability]   build labelled grids (8/board) of archived images for eyeballing
  stats                 print ledger status counts
"""
import json, sys, io, re, time, urllib.request
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
LED = ROOT / "tools" / "recipe_image_ledger.json"
ARCH = ROOT / "image_archive"; ARCH.mkdir(exist_ok=True)
MON = ROOT / "image_archive" / "_montages"; MON.mkdir(exist_ok=True)
UA = "HealthOptimizerRecipeImages/1.0 (dish photo lookup; jamesdmiller9@gmail.com)"

def load(): return json.loads(LED.read_text(encoding="utf-8"))
def save(l): LED.write_text(json.dumps(l, ensure_ascii=False, indent=0), encoding="utf-8")
def slug(s): return re.sub(r"-+", "-", re.sub(r"[^a-z0-9]+", "-", s.lower())).strip("-")[:48]

def archive(stability="stable"):
    led = load()
    rows = [x for x in led if x["stability"] == stability and x["url"]]
    ok = 0
    for i, x in enumerate(rows):
        fn = f"{x['id']:04d}_{slug(x['name'])}.jpg"
        p = ARCH / fn
        if p.exists() and p.stat().st_size > 500:
            x["archive_file"] = fn; x["downloaded"] = True; ok += 1; continue
        got = False
        for a in range(3):
            try:
                d = urllib.request.urlopen(urllib.request.Request(x["url"], headers={"User-Agent": UA}), timeout=30).read()
                if len(d) > 500: p.write_bytes(d); got = True; break
            except Exception: time.sleep(1.0)
        x["archive_file"] = fn if got else ""; x["downloaded"] = got
        if got: ok += 1
        else: x["note"] = (x.get("note") or "") + "[stable url dead] "
        time.sleep(0.2)
        if (i+1) % 60 == 0:
            save(led); print(f"{i+1}/{len(rows)} ({ok} archived)", flush=True)
    save(led)
    print(f"archived {ok}/{len(rows)} {stability}; {len(rows)-ok} dead -> needs_fix", flush=True)

def montage(stability="stable", per=8):
    led = load()
    rows = [x for x in led if x["stability"] == stability and x.get("downloaded")]
    try: font = ImageFont.truetype("arial.ttf", 13); fb = ImageFont.truetype("arialbd.ttf", 16)
    except Exception: font = fb = ImageFont.load_default()
    def asc(s): return s.encode("ascii", "replace").decode()
    CELL, LBL = 300, 320
    n = (len(rows)+per-1)//per
    for m in range(n):
        batch = rows[m*per:(m+1)*per]
        mo = Image.new("RGB", (LBL+CELL, CELL*len(batch)), (16,16,20)); d = ImageDraw.Draw(mo)
        for r, x in enumerate(batch):
            y = r*CELL
            try: mo.paste(Image.open(ARCH / x["archive_file"]).convert("RGB").resize((CELL,CELL)), (LBL, y))
            except Exception: pass
            d.text((6, y+8), str(x["id"]), fill=(120,220,255), font=fb)
            words = asc(x["name"]).split(); line=""; yy=y+32
            for w in words:
                if len(line)+len(w) > 32: d.text((8,yy),line,fill=(255,255,255),font=font); yy+=18; line=w
                else: line=(line+" "+w).strip()
            d.text((8,yy),line,fill=(255,255,255),font=font)
            d.text((8, y+CELL-22), asc(x["category"])[:36], fill=(150,150,150), font=font)
        mo.save(MON / f"{stability}_{m:04d}.png")
    print(f"wrote {n} montages to image_archive/_montages/", flush=True)

def stats():
    led = load()
    from collections import Counter
    print("status:", dict(Counter(x["status"] for x in led)))
    print("stability:", dict(Counter(x["stability"] for x in led)))
    print("downloaded:", sum(1 for x in led if x.get("downloaded")))

if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "stats"
    arg = sys.argv[2] if len(sys.argv) > 2 else "stable"
    {"archive": lambda: archive(arg), "montage": lambda: montage(arg), "stats": stats}[cmd]()

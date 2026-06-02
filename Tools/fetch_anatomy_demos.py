"""
Replaces the Free Exercise DB photo-based bundle with everkinetic's anatomy-art bundle.
everkinetic ships 2 PNGs per exercise (tension + relaxation poses, ~30-50KB each).
We combine them into a 2-frame animated GIF so the anatomy alternates between start/end
positions — animated motion in anatomical line-art style.

Usage:  python Tools/fetch_anatomy_demos.py
Requires: seed_names.txt in repo root, Pillow (pip install Pillow).
Source: https://github.com/everkinetic/data  (CC-BY-SA 4.0)
"""
import io
import json
import os
import re
import sys
import urllib.request
from difflib import SequenceMatcher

try:
    from PIL import Image
except ImportError:
    print("Pillow required: pip install Pillow")
    sys.exit(1)

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NAMES_PATH = os.path.join(REPO_ROOT, "seed_names.txt")
OUT_DIR = os.path.join(REPO_ROOT, "Resources", "Raw", "exercise_demos")
MAP_PATH = os.path.join(REPO_ROOT, "Data", "ExerciseMediaMap.cs")
EVERKINETIC_INDEX_URL = "https://raw.githubusercontent.com/everkinetic/data/master/exercises.json"
PNG_BASE = "https://raw.githubusercontent.com/everkinetic/data/master/dist/png"

# Words to strip when matching seed names against everkinetic's catalog
PREFIXES = ["Dumbbell", "Barbell", "Cable", "Machine", "Band", "Resistance Band",
            "Smith", "Goblet", "Single-Arm", "Single-Leg", "TRX", "Kettlebell",
            "Suspended", "BOSU", "Stability Ball", "Medicine Ball", "Plyo",
            "Box", "CES"]

# Manual overrides — seed_name → everkinetic id_num. Used when fuzzy matching misses
# or picks a worse candidate. Everkinetic uses unusual naming for staples.
MANUAL_MAPPINGS = {
    # Pulls
    "Pull-Up": "0087",                          # Pull Ups
    "Chin-Up": "0089",                          # Chin Ups
    "Lat Pulldown": "0097",                     # Wide Grip Lat Pull Down
    "Cable Row": "0025",                        # Seated Cable Rows
    "Cable Row with Chin Tuck": "0025",         # Seated Cable Rows
    "Barbell Row": "0298",                      # Bent Over Row with Barbell
    "Band Row": "0094",                         # Upright Band Rows
    "Dumbbell Row": "0298",                     # Bent Over Row (no dumbbell variant)
    # Hinges
    "Barbell Deadlift": "0303",                 # Dead Lifts Smith Machine
    # Overhead Press
    "Overhead Press": "0011",                   # Seated Barbell Shoulder Press
    "Barbell Overhead Press": "0011",           # Seated Barbell Shoulder Press
    "Band Shoulder Press": "0012",              # Dumbbell Shoulder Press (closest non-machine)
    # Chest
    "Dumbbell Chest Fly": "0056",               # Dumbbell Flys
    "Cable Chest Fly": "0057",                  # Flat Bench Cable Flys
    "Diamond Push-Up": "0188",                  # Close Triceps Pushup
    # Tris
    "Tricep Dip": "0172",                       # Tricep Dips using Body Weight
    # Legs
    "Dumbbell Lunge": "0115",                   # Dumbbell Lunges
    "Reverse Lunge": "0128",                    # Rear Lunges with Barbell
    "Bodyweight Leg Curl": "0117",              # Lying Leg Curl Machine (closest)
    # Curls
    "Bicep Curl": "0223",                       # Alternating Bicep Curl with Dumbbell
    "Cable Bicep Curl": "0247",                 # Standing One Arm Bicep Curl with Cable
    "Band Bicep Curl": "0223",                  # Alternating Bicep Curl (no band variant)
    # Core
    "Superman": "0103",                         # Hyperextensions (closest)
}

STOPWORDS = {"and", "or", "the", "a", "an", "with", "to", "of", "on", "in",
             "stretch", "exercise"}

WEAK_TOKENS = {"bodyweight", "cable", "barbell", "dumbbell", "machine", "band",
               "kettlebell", "standing", "seated", "lying", "supine", "prone",
               "kneeling", "single", "double", "smith", "wall", "doorway",
               "ces", "smr", "stretch", "raise", "pull", "push", "press",
               "curl", "row", "fly", "flyes", "extension", "squeeze"}

def normalize(s):
    s = s.replace("’", "'").replace("—", "-")
    s = re.sub(r"[^\w\s/-]", " ", s)
    s = re.sub(r"\s+", " ", s).strip().lower()
    return s

def strip_prefixes(s):
    out = s
    changed = True
    while changed:
        changed = False
        for p in PREFIXES:
            if out.lower().startswith(p.lower() + " "):
                out = out[len(p) + 1:]
                changed = True
    return out

def tokens(s):
    n = normalize(s)
    glued = n.replace("-", "")
    combined = n + " " + glued
    return {w for w in re.split(r"[\s/_-]+", combined) if w and w not in STOPWORDS}

def strong_tokens(s):
    return {w for w in tokens(s) if w not in WEAK_TOKENS}

def similarity(a, b):
    return SequenceMatcher(None, normalize(a), normalize(b)).ratio()

def find_match(seed_name, index):
    norm_seed = normalize(seed_name)
    norm_stripped = normalize(strip_prefixes(seed_name))

    for e in index:
        if normalize(e["title"]) == norm_seed:
            return e, "exact"
    for e in index:
        if normalize(e["title"]) == norm_stripped:
            return e, "stripped"

    seed_strong = strong_tokens(strip_prefixes(seed_name))
    best, best_score = None, 0.0
    for e in index:
        cand = e["title"]
        shared_strong = seed_strong & strong_tokens(cand)
        shared_all = tokens(strip_prefixes(seed_name)) & tokens(cand)
        if not shared_strong and len(shared_all) < 2:
            continue
        score = max(similarity(seed_name, cand),
                    similarity(strip_prefixes(seed_name), cand))
        if score > best_score:
            best, best_score = e, score

    if best_score >= 0.65:
        return best, f"fuzzy:{best_score:.2f}"
    return None, "no-match"

def safe_filename(seed_name):
    s = re.sub(r"[^\w]+", "_", seed_name.lower()).strip("_")
    return s + ".gif"

def download_bytes(url, timeout=30):
    with urllib.request.urlopen(url, timeout=timeout) as resp:
        return resp.read()

def build_animated_gif(image_urls, out_path, frame_duration_ms=600):
    frames = []
    for url in image_urls:
        data = download_bytes(url)
        img = Image.open(io.BytesIO(data)).convert("RGBA")
        # everkinetic art has transparent background; flatten to white for cleaner display
        bg = Image.new("RGB", img.size, (255, 255, 255))
        bg.paste(img, mask=img.split()[3] if len(img.split()) > 3 else None)
        bg.thumbnail((360, 360))
        bg = bg.convert("P", palette=Image.Palette.ADAPTIVE, colors=128)
        frames.append(bg)
    if not frames:
        return False
    frames[0].save(out_path, save_all=True, append_images=frames[1:],
                   duration=frame_duration_ms, loop=0, optimize=True, disposal=2)
    return True

def main():
    if not os.path.exists(NAMES_PATH):
        print(f"missing {NAMES_PATH}")
        sys.exit(1)

    print(f"fetching everkinetic index from {EVERKINETIC_INDEX_URL}")
    index_data = download_bytes(EVERKINETIC_INDEX_URL)
    index = json.loads(index_data.decode("utf-8"))
    print(f"everkinetic catalog has {len(index)} exercises")

    with open(NAMES_PATH, "r", encoding="utf-8") as f:
        seed_names = [ln.strip() for ln in f if ln.strip()]

    os.makedirs(OUT_DIR, exist_ok=True)

    mapping = {}
    matched, missing = 0, 0

    # Build id_num → entry lookup for manual overrides
    by_id_num = {e["id_num"]: e for e in index if "id_num" in e}

    for seed in seed_names:
        # Manual override wins
        if seed in MANUAL_MAPPINGS:
            override_id = MANUAL_MAPPINGS[seed]
            if override_id in by_id_num:
                match, reason = by_id_num[override_id], "manual"
            else:
                print(f"  !! {seed!r} manual override {override_id!r} not in index")
                match, reason = None, "manual-missing"
        else:
            match, reason = find_match(seed, index)

        if match is None:
            mapping[seed] = None
            missing += 1
            print(f"  -- {seed!r}  no match")
            continue

        id_num = match.get("id_num")
        if not id_num:
            mapping[seed] = None
            missing += 1
            continue

        urls = [f"{PNG_BASE}/{id_num}-tension.png", f"{PNG_BASE}/{id_num}-relaxation.png"]
        local_name = safe_filename(seed)
        local_full = os.path.join(OUT_DIR, local_name)

        if not os.path.exists(local_full):
            try:
                build_animated_gif(urls, local_full)
            except Exception as ex:
                print(f"  !! {seed!r} build failed: {ex}")
                mapping[seed] = None
                missing += 1
                continue

        mapping[seed] = local_name
        matched += 1
        print(f"  ok {seed!r}  ->  {match['title']!r}  ({reason})  =>  {local_name}")

    with open(MAP_PATH, "w", encoding="utf-8") as f:
        f.write("// Auto-generated by Tools/fetch_anatomy_demos.py — do not edit by hand.\n")
        f.write("// Maps seed exercise names to bundled anatomy-art animation files.\n")
        f.write("// Resources/Raw/exercise_demos/. CC-BY-SA 4.0 content from everkinetic.\n\n")
        f.write("namespace IntelligentPersonalHealthOptimization.Data;\n\n")
        f.write("public static class ExerciseMediaMap\n{\n")
        f.write("    public static readonly Dictionary<string, string> NameToImageFile = new()\n    {\n")
        for k, v in sorted(mapping.items()):
            if v is None:
                continue
            f.write(f"        [{json.dumps(k)}] = {json.dumps(v)},\n")
        f.write("    };\n}\n")

    print()
    print(f"matched: {matched}   missing: {missing}   total: {len(seed_names)}")
    print(f"images: {OUT_DIR}")
    print(f"map:    {MAP_PATH}")

if __name__ == "__main__":
    main()

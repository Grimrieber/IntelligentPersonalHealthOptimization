"""
One-time content tool: matches our seed exercise names to the Free Exercise DB
(MIT-licensed) and downloads BOTH images per exercise (start + end positions),
combines them into a 2-frame animated GIF, and writes the name->filename map.

Usage:  python Tools/fetch_exercise_demos.py
Requires: exercises_index.json (Free Exercise DB), seed_names.txt, and Pillow
          (pip install Pillow).
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
INDEX_PATH = os.path.join(REPO_ROOT, "exercises_index.json")
NAMES_PATH = os.path.join(REPO_ROOT, "seed_names.txt")
OUT_DIR = os.path.join(REPO_ROOT, "Resources", "Raw", "exercise_demos")
MAP_PATH = os.path.join(REPO_ROOT, "Data", "ExerciseMediaMap.cs")

CDN_BASE = "https://raw.githubusercontent.com/yuhonas/free-exercise-db/main/exercises"

# Words to strip when matching (e.g. "Dumbbell Bench Press" -> "Bench Press")
PREFIXES = ["Dumbbell", "Barbell", "Cable", "Machine", "Band", "Resistance Band",
            "Smith", "Goblet", "Single-Arm", "Single-Leg", "TRX", "Kettlebell",
            "Suspended", "BOSU", "Stability Ball", "Medicine Ball", "Plyo",
            "Box", "CES"]

def normalize(s):
    s = s.replace("’", "'").replace("—", "-")
    # Drop punctuation we don't want to match on
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

STOPWORDS = {"and", "or", "the", "a", "an", "with", "to", "of", "on", "in",
             "stretch", "exercise"}

# Tokens that are too generic to identify an exercise on their own. They CAN
# count toward a match when combined with a stronger token, but cannot be the
# only shared token. This is what blocked false positives like
# "Bodyweight Leg Curl" matching "Bodyweight Flyes" on just "bodyweight".
WEAK_TOKENS = {"bodyweight", "cable", "barbell", "dumbbell", "machine", "band",
               "kettlebell", "standing", "seated", "lying", "supine", "prone",
               "kneeling", "single", "double", "smith", "wall", "doorway",
               "ces", "smr", "stretch", "raise", "pull", "push", "press",
               "curl", "row", "fly", "flyes", "extension", "squeeze"}

def tokens(s):
    """Tokenize, AND include hyphen-glued variants so 'Push-Ups' matches 'Pushups'."""
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
    norm_seed_stripped = normalize(strip_prefixes(seed_name))

    # Exact match (normalized) — full or stripped seed
    for e in index:
        if normalize(e["name"]) == norm_seed:
            return e, "exact"
    for e in index:
        if normalize(e["name"]) == norm_seed_stripped:
            return e, "stripped"

    seed_for_compare = strip_prefixes(seed_name)
    seed_strong = strong_tokens(seed_for_compare)

    best, best_score = None, 0.0
    for e in index:
        cand = e["name"]
        cand_tokens = tokens(cand)
        cand_strong = strong_tokens(cand)

        shared_strong = seed_strong & cand_strong
        shared_all = tokens(seed_for_compare) & cand_tokens

        # Need EITHER a strong shared token (muscle/movement name like "adductor", "deadlift")
        # OR at least 2 shared tokens overall (compensates for weak-only overlap).
        if not shared_strong and len(shared_all) < 2:
            continue

        score = max(similarity(seed_name, cand), similarity(seed_for_compare, cand))
        if score > best_score:
            best, best_score = e, score

    if best_score >= 0.65:
        return best, f"fuzzy:{best_score:.2f}"
    return None, "no-match"

def safe_filename(seed_name):
    s = re.sub(r"[^\w]+", "_", seed_name.lower()).strip("_")
    return s + ".gif"

def download_bytes(url):
    with urllib.request.urlopen(url, timeout=30) as resp:
        return resp.read()

def build_animated_gif(image_urls, out_path, frame_duration_ms=600):
    """Download N images and combine into an animated GIF that loops between frames.
    Aggressive size reduction: 360px max + adaptive 128-color palette."""
    frames = []
    for url in image_urls:
        data = download_bytes(url)
        img = Image.open(io.BytesIO(data)).convert("RGB")
        img.thumbnail((360, 360))
        # Quantize to a shared 128-color palette so GIF stays small
        img = img.convert("P", palette=Image.Palette.ADAPTIVE, colors=128)
        frames.append(img)
    if not frames:
        return False
    if len(frames) == 1:
        frames[0].save(out_path, optimize=True)
        return True
    frames[0].save(
        out_path,
        save_all=True,
        append_images=frames[1:],
        duration=frame_duration_ms,
        loop=0,
        optimize=True,
        disposal=2
    )
    return True

def main():
    if not os.path.exists(INDEX_PATH):
        print(f"missing {INDEX_PATH} — run: curl -sL https://raw.githubusercontent.com/yuhonas/free-exercise-db/main/dist/exercises.json -o exercises_index.json")
        sys.exit(1)
    if not os.path.exists(NAMES_PATH):
        print(f"missing {NAMES_PATH}")
        sys.exit(1)

    with open(INDEX_PATH, "r", encoding="utf-8") as f:
        index = json.load(f)
    with open(NAMES_PATH, "r", encoding="utf-8") as f:
        seed_names = [ln.strip() for ln in f if ln.strip()]

    os.makedirs(OUT_DIR, exist_ok=True)

    mapping = {}    # seed_name -> local_filename (or None)
    matched, missing = 0, 0

    for seed in seed_names:
        match, reason = find_match(seed, index)
        if match is None or not match.get("images"):
            mapping[seed] = None
            missing += 1
            print(f"  -- {seed!r}  no match")
            continue

        # Build a 2-frame animated GIF from start + end stills
        urls = [f"{CDN_BASE}/{p}" for p in match["images"][:2]]
        local_name = safe_filename(seed)
        local_full = os.path.join(OUT_DIR, local_name)

        if not os.path.exists(local_full):
            try:
                build_animated_gif(urls, local_full)
            except Exception as ex:
                print(f"  !! {seed!r} download/encode failed: {ex}")
                mapping[seed] = None
                missing += 1
                continue

        mapping[seed] = local_name
        matched += 1
        print(f"  ok {seed!r}  ->  {match['name']!r}  ({reason})  =>  {local_name}")

    # Write C# lookup map
    with open(MAP_PATH, "w", encoding="utf-8") as f:
        f.write("// Auto-generated by Tools/fetch_exercise_demos.py — do not edit by hand.\n")
        f.write("// Maps seed exercise names to local demonstration image filenames in\n")
        f.write("// Resources/Raw/exercise_demos/. MIT-licensed content from Free Exercise DB.\n\n")
        f.write("namespace IntelligentPersonalHealthOptimization.Data;\n\n")
        f.write("public static class ExerciseMediaMap\n{\n")
        f.write("    public static readonly Dictionary<string, string> NameToImageFile = new()\n    {\n")
        for k, v in sorted(mapping.items()):
            if v is None:
                continue
            f.write(f"        [{json.dumps(k)}] = {json.dumps(v)},\n")
        f.write("    };\n}\n")

    print(f"\nmatched: {matched}   missing: {missing}   total: {len(seed_names)}")
    print(f"images: {OUT_DIR}")
    print(f"map:    {MAP_PATH}")

if __name__ == "__main__":
    main()

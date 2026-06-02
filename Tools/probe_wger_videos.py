"""
Probe wger.de to find how many of our seed exercises have video coverage.
Read-only — does not modify the app.
"""
import json
import os
import time
import urllib.request
import urllib.parse

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NAMES_PATH = os.path.join(REPO_ROOT, "seed_names.txt")
WGER = "https://wger.de/api/v2"

def http_json(url):
    with urllib.request.urlopen(url, timeout=15) as r:
        return json.loads(r.read())

def find_exercise_id(name):
    # Try exact name first
    url = f"{WGER}/exercise-translation/?language=2&limit=1&name={urllib.parse.quote(name)}"
    d = http_json(url)
    if d["results"]:
        return d["results"][0]["exercise"], "exact"
    # Then search
    url = f"{WGER}/exercise-translation/?language=2&limit=1&search={urllib.parse.quote(name)}"
    d = http_json(url)
    if d["results"]:
        return d["results"][0]["exercise"], "search"
    return None, None

def has_video(exercise_id):
    url = f"{WGER}/video/?exercise={exercise_id}&limit=1"
    d = http_json(url)
    return d["count"] > 0

with open(NAMES_PATH) as f:
    names = [ln.strip() for ln in f if ln.strip()]

video_hits = []
no_match = 0
for name in names:
    try:
        eid, how = find_exercise_id(name)
        if not eid:
            no_match += 1
            continue
        if has_video(eid):
            video_hits.append((name, eid))
            print(f"  VIDEO: {name!r} -> wger#{eid}")
        time.sleep(0.15)
    except Exception as ex:
        print(f"  err {name!r}: {ex}")

print()
print(f"total seed names:     {len(names)}")
print(f"wger no-match:        {no_match}")
print(f"wger match w/ video:  {len(video_hits)}")

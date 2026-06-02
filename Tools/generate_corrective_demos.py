"""
Generates simple stick-figure pose animations for corrective / stretch exercises that
everkinetic doesn't cover. Each exercise = 2 PNG frames combined into a looping GIF.

Style: black lines on white, single human figure + equipment indicator where relevant.
Drawn programmatically via PIL primitives — not photorealistic but communicates the
pose change. Sample batch only; review before extending to all 20 staples.

Usage:  python Tools/generate_corrective_demos.py
"""
import os
from PIL import Image, ImageDraw

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(REPO_ROOT, "Resources", "Raw", "exercise_demos")
MAP_PATH = os.path.join(REPO_ROOT, "Data", "ExerciseMediaMap.cs")

W, H = 360, 360
LINE = 4
COLOR = (30, 30, 30)
BG = (255, 255, 255)

# ---------- primitives ----------
def new_canvas():
    img = Image.new("RGB", (W, H), BG)
    return img, ImageDraw.Draw(img)

def stick(draw, head_xy, shoulder_xy, hip_xy, knee_xy, foot_xy, elbow_xy, hand_xy,
          other_knee_xy=None, other_foot_xy=None, other_elbow_xy=None, other_hand_xy=None,
          head_r=14):
    # Head
    hx, hy = head_xy
    draw.ellipse((hx - head_r, hy - head_r, hx + head_r, hy + head_r), outline=COLOR, width=LINE)
    # Spine
    draw.line([shoulder_xy, hip_xy], fill=COLOR, width=LINE)
    # Neck
    draw.line([head_xy, shoulder_xy], fill=COLOR, width=LINE)
    # Arm
    draw.line([shoulder_xy, elbow_xy], fill=COLOR, width=LINE)
    draw.line([elbow_xy, hand_xy], fill=COLOR, width=LINE)
    if other_elbow_xy and other_hand_xy:
        draw.line([shoulder_xy, other_elbow_xy], fill=COLOR, width=LINE)
        draw.line([other_elbow_xy, other_hand_xy], fill=COLOR, width=LINE)
    # Leg
    draw.line([hip_xy, knee_xy], fill=COLOR, width=LINE)
    draw.line([knee_xy, foot_xy], fill=COLOR, width=LINE)
    if other_knee_xy and other_foot_xy:
        draw.line([hip_xy, other_knee_xy], fill=COLOR, width=LINE)
        draw.line([other_knee_xy, other_foot_xy], fill=COLOR, width=LINE)

def foam_roller(draw, cx, cy, w=120, h=18):
    draw.rounded_rectangle((cx - w/2, cy - h/2, cx + w/2, cy + h/2),
                           radius=h/2, outline=COLOR, width=LINE)

def ground_line(draw, y=340):
    draw.line([(20, y), (W - 20, y)], fill=COLOR, width=LINE)
    for x in range(40, W - 20, 30):
        draw.line([(x, y + 2), (x - 8, y + 14)], fill=COLOR, width=2)

# ---------- exercise frames ----------
# Each function returns two PIL Images: (frame_0, frame_1)

def glute_bridge():
    """Supine — hips down (frame 0) vs hips up (frame 1)."""
    out = []
    for hip_y in (290, 230):
        img, d = new_canvas()
        ground_line(d)
        # Head left, lying flat
        head = (80, 305)
        shoulder = (115, 305)
        hip = (200, hip_y)
        knee = (260, hip_y + 10 if hip_y > 250 else 240)
        foot = (290, 335)
        # Arm flat by side
        elbow = (135, 320)
        hand = (165, 320)
        # Other leg
        ok = knee; of = foot  # both legs together
        stick(d, head, shoulder, hip, knee, foot, elbow, hand)
        # Render the second knee/foot identical for "bilateral"
        out.append(img)
    return out

def plank():
    """Prone forearm plank — hold pose with slight body sag toggle to show alignment."""
    out = []
    for sag in (0, 8):
        img, d = new_canvas()
        ground_line(d)
        # Head right, body angled down
        head = (290, 240 + sag)
        shoulder = (255, 250 + sag)
        hip = (160, 270 + sag)
        knee = (110, 290 + sag/2)
        foot = (60, 320)
        elbow = (260, 295)
        hand = (260, 320)
        stick(d, head, shoulder, hip, knee, foot, elbow, hand)
        out.append(img)
    return out

def foam_roll_it_band():
    """Side-lying with foam roller under outer thigh; roll position 1 vs position 2."""
    out = []
    for roller_x in (180, 250):
        img, d = new_canvas()
        ground_line(d)
        # Roller
        foam_roller(d, roller_x, 320)
        # Side-lying figure
        head = (90, 240)
        shoulder = (130, 250)
        hip = (210, 280)
        knee = (260, 305)
        foot = (300, 305)
        # Top arm propping
        elbow = (110, 290)
        hand = (110, 320)
        other_elbow = (150, 220)
        other_hand = (180, 200)
        stick(d, head, shoulder, hip, knee, foot, elbow, hand,
              other_elbow_xy=other_elbow, other_hand_xy=other_hand)
        out.append(img)
    return out

def hip_flexor_stretch():
    """Kneeling lunge — neutral vs hip pushed forward."""
    out = []
    for forward in (0, 18):
        img, d = new_canvas()
        ground_line(d)
        head = (180 + forward, 110)
        shoulder = (180 + forward, 140)
        hip = (180 + forward, 220)
        # Front leg bent 90deg
        knee = (250 + forward, 280)
        foot = (250 + forward, 335)
        # Back leg kneeling
        other_knee = (130, 335)
        other_foot = (90, 335)
        # Arms on front thigh
        elbow = (210 + forward, 200)
        hand = (240 + forward, 250)
        other_elbow = (150 + forward, 200)
        other_hand = (180 + forward, 250)
        stick(d, head, shoulder, hip, knee, foot, elbow, hand,
              other_knee_xy=other_knee, other_foot_xy=other_foot,
              other_elbow_xy=other_elbow, other_hand_xy=other_hand)
        out.append(img)
    return out

def bird_dog():
    """All fours — opposite arm + leg extended (frame 1) vs neutral (frame 0)."""
    out = []
    for extend in (False, True):
        img, d = new_canvas()
        ground_line(d)
        head = (290, 200)
        shoulder = (255, 215)
        hip = (140, 215)
        # Front leg supporting
        knee = (140, 290)
        foot = (115, 335)
        # Back leg — extended in frame 1, supporting in frame 0
        if extend:
            other_knee = (95, 240)
            other_foot = (40, 200)  # extended back & up
        else:
            other_knee = (165, 290)
            other_foot = (190, 335)
        # Support arm
        elbow = (255, 290)
        hand = (255, 335)
        # Opposite arm extended in frame 1
        if extend:
            other_elbow = (310, 170)
            other_hand = (345, 140)
        else:
            other_elbow = (225, 290)
            other_hand = (225, 335)
        stick(d, head, shoulder, hip, knee, foot, elbow, hand,
              other_knee_xy=other_knee, other_foot_xy=other_foot,
              other_elbow_xy=other_elbow, other_hand_xy=other_hand)
        out.append(img)
    return out

# ---------- build GIFs ----------

EXERCISES = {
    "Glute Bridge": glute_bridge,
    "Plank": plank,                          # may already exist from everkinetic; will overwrite if so
    "Foam Roll IT Band": foam_roll_it_band,
    "Hip Flexor Stretch": hip_flexor_stretch,
    "Bird-Dog": bird_dog,
}

def safe_filename(seed_name):
    import re
    s = re.sub(r"[^\w]+", "_", seed_name.lower()).strip("_")
    return s + ".gif"

def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    new_entries = {}
    for name, fn in EXERCISES.items():
        frames = fn()
        # Convert to palette mode for GIF
        palette_frames = [f.convert("P", palette=Image.Palette.ADAPTIVE, colors=64) for f in frames]
        filename = safe_filename(name)
        path = os.path.join(OUT_DIR, filename)
        palette_frames[0].save(
            path, save_all=True, append_images=palette_frames[1:],
            duration=700, loop=0, optimize=True, disposal=2
        )
        new_entries[name] = filename
        print(f"  ok {name!r} -> {filename}")

    # Merge into existing ExerciseMediaMap.cs (don't overwrite the everkinetic entries)
    import re
    with open(MAP_PATH, "r", encoding="utf-8") as f:
        contents = f.read()
    # Parse existing entries
    pattern = re.compile(r'\["([^"]+)"\]\s*=\s*"([^"]+)"', re.MULTILINE)
    existing = dict(pattern.findall(contents))
    for k, v in new_entries.items():
        existing[k] = v
    # Re-emit file
    with open(MAP_PATH, "w", encoding="utf-8") as f:
        f.write("// Auto-generated — do not edit by hand.\n")
        f.write("// Map of seed exercise name → bundled animation file.\n")
        f.write("// Sources: everkinetic (CC-BY-SA), hand-coded corrective stick figures.\n\n")
        f.write("namespace IntelligentPersonalHealthOptimization.Data;\n\n")
        f.write("public static class ExerciseMediaMap\n{\n")
        f.write("    public static readonly Dictionary<string, string> NameToImageFile = new()\n    {\n")
        for k, v in sorted(existing.items()):
            import json as _j
            f.write(f"        [{_j.dumps(k)}] = {_j.dumps(v)},\n")
        f.write("    };\n}\n")

    print()
    print(f"added: {len(new_entries)}   total bundled: {len(existing)}")

if __name__ == "__main__":
    main()

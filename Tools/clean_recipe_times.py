"""
Clean up scraped Wikibooks time fields. The scrape occasionally dumped the whole
"Prep: X Cooking: Y Total: Z" block into a single cookTime string with no separators
(e.g. "Prep: 10 minutesCooking: 20 minutesTotal: 30 minutes"), which renders as one
jammed line on the recipe card. This redistributes the labelled segments back into the
proper prepTime / cookTime / restTime fields, drops redundant Total/Ready, fixes the
U+FFFD mojibake (originally en-dash / fraction glyphs), and de-glues residual camel-case.

Usage:  python tools/clean_recipe_times.py preview   # counts + sample, no write
        python tools/clean_recipe_times.py apply     # rewrite bundle, bump schemaVersion
"""
import gzip, json, re, sys
from pathlib import Path

BUNDLE = Path(__file__).resolve().parent.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"

LABELS = (r'Prep(?:aration)?|Cook(?:ing)?|Bak(?:e|ing)|Grill(?:ing)?|Fry(?:ing)?|Roast(?:ing)?|'
          r'Simmer(?:ing)?|Boil(?:ing)?|Steam(?:ing)?|Braising|Total|Rest(?:ing)?|Chill(?:ing)?|'
          r'Cool(?:ing)?|Refrigerat(?:e|ion)|Soak(?:ing)?|Stand(?:ing)?|Ris(?:e|ing)|Prov(?:e|ing)|'
          r'Proof(?:ing)?|Marinat(?:e|ing|ion)?|Freez(?:e|ing)|Set(?:ting)?|Ferment(?:ation|ing)?|'
          r'Thaw(?:ing)?|Assembly|Assembl(?:e|ing)|Mak(?:e|ing)|Steep(?:ing)?|Drain(?:ing)?|'
          r'Dry(?:ing)?|Cur(?:e|ing)|Brin(?:e|ing)|Ready(?:\s*in)?|Active|Inactive')
SEG = re.compile(r'(?P<label>' + LABELS + r')\s*:\s*', re.I)


def classify(label):
    l = label.lower()
    if l.startswith('prep') or l == 'active' or l.startswith('assembl') or l.startswith('mak'):
        return 'prep'
    if l.startswith(('cook', 'bak', 'grill', 'fry', 'roast', 'simmer', 'boil', 'steam', 'brais')):
        return 'cook'
    if l == 'total' or l.startswith('ready'):
        return 'skip'
    return 'rest'


def tidy(v):
    v = v.replace('�', '-')
    v = re.sub(r'(minutes?|hours?|days?|seconds?|weeks?|months?|overnight)([A-Z])', r'\1 \2', v)
    return re.sub(r'\s+', ' ', v).strip().strip(',;').strip()


def parse(blob):
    if not isinstance(blob, str):
        return None
    matches = list(SEG.finditer(blob))
    if not matches:
        return None
    out = {'prep': [], 'cook': [], 'rest': []}
    for i, m in enumerate(matches):
        start = m.end()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(blob)
        val = tidy(blob[start:end])
        tgt = classify(m.group('label'))
        if tgt != 'skip' and val:
            out[tgt].append(val)
    return {k: ' - '.join(v) for k, v in out.items() if v}  # ' - ' avoids the middot glyph


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else 'preview'
    data = json.load(gzip.open(BUNDLE, 'rt', encoding='utf-8'))
    recs = data['recipes'] if isinstance(data, dict) else data
    changed = 0
    for r in recs:
        parsed = parse(r.get('cookTime') or '')
        if not parsed:
            continue
        changed += 1
        if mode == 'apply':
            r['prepTime'] = parsed.get('prep') or None
            r['cookTime'] = parsed.get('cook') or None
            r['restTime'] = parsed.get('rest') or None
    print(f"redistributed {changed} recipes")
    if mode == 'apply':
        if isinstance(data, dict):
            data['schemaVersion'] = int(data.get('schemaVersion', 1)) + 1
            print(f"bundle schemaVersion -> {data['schemaVersion']}")
        with gzip.open(BUNDLE, 'wt', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False)
        print("wrote bundle")


if __name__ == '__main__':
    main()

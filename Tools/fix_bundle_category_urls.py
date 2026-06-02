"""
One-shot fix: strip leaked URL params (e.g. '&action=edit&redlink=1') from
category names inside Resources/Raw/wikibooks_bundle.json.gz. Regenerates
the gzip in place so future installs seed clean data.
"""
import gzip
import json
import re
from pathlib import Path

BUNDLE = Path(__file__).parent.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"


def clean(name: str) -> str:
    if not name:
        return name
    # Truncate at first '&' or '?'
    for sep in ("&", "?"):
        if sep in name:
            name = name.split(sep, 1)[0]
    return re.sub(r"\s+", " ", name).strip()


def main():
    with gzip.open(BUNDLE, "rb") as f:
        data = json.loads(f.read().decode("utf-8"))

    changed = 0
    seen_old = set()
    for r in data["recipes"]:
        cat = r.get("category", "")
        new = clean(cat)
        if new != cat:
            if cat not in seen_old:
                print(f"  {cat!r}  ->  {new!r}")
                seen_old.add(cat)
            r["category"] = new
            changed += 1

    # Also clean the top-level categories array
    data["categories"] = sorted({clean(c) for c in data.get("categories", [])})

    raw = json.dumps(data, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    with gzip.open(BUNDLE, "wb", compresslevel=9) as f:
        f.write(raw)
    size_mb = BUNDLE.stat().st_size / 1024 / 1024
    print(f"\nUpdated {changed} recipe.category fields. Bundle is {size_mb:.2f} MB.")


if __name__ == "__main__":
    main()

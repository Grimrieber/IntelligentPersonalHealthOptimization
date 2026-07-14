"""
Remove the 107 approved same-dish duplicate recipes (tools/delete_list.json) from
BOTH the shipped bundle (Resources/Raw/wikibooks_bundle.json.gz) and the image
ledger (tools/recipe_image_ledger.json). Match by exact recipe NAME (the bundle
has no id; ledger id -> name).

Also emits tools/deleted_names.json (the exact name list) for the on-device
catalog-resync migration to confirm against.

Usage:  python tools/delete_recipes.py            # dry run (report only)
        python tools/delete_recipes.py apply      # write changes
"""
import gzip, json, io, os, sys
from pathlib import Path

HERE   = Path(__file__).resolve().parent
BUNDLE = HERE.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"
LED    = HERE / "recipe_image_ledger.json"
DL     = HERE / "delete_list.json"
NAMES_OUT = HERE / "deleted_names.json"

def main():
    apply = len(sys.argv) > 1 and sys.argv[1] == "apply"
    dl = json.load(io.open(DL, encoding="utf-8"))
    del_ids   = {d["id"] for d in dl["delete"]}
    del_names = {d["name"] for d in dl["delete"]}
    print(f"delete list: {len(del_ids)} ids / {len(del_names)} names")

    led = json.load(io.open(LED, encoding="utf-8"))
    led_names_for_ids = {r["name"] for r in led if r["id"] in del_ids}
    # sanity: names from ledger-by-id must match names in delete list
    mism = del_names ^ led_names_for_ids
    if mism:
        print(f"WARNING: {len(mism)} name mismatches between delete list and ledger:")
        for m in sorted(mism):
            print("   ", m)

    b = json.load(gzip.open(BUNDLE, "rt", encoding="utf-8"))
    recipes = b["recipes"]
    before = len(recipes)
    kept   = [r for r in recipes if r["name"] not in del_names]
    removed = before - len(kept)
    print(f"bundle: {before} -> {len(kept)} recipes  (removed {removed})")

    led_before = len(led)
    led_kept = [r for r in led if r["id"] not in del_ids and r["name"] not in del_names]
    print(f"ledger: {led_before} -> {len(led_kept)} entries  (removed {led_before-len(led_kept)})")

    not_found = del_names - {r["name"] for r in recipes}
    if not_found:
        print(f"WARNING: {len(not_found)} delete-names not present in bundle:")
        for m in sorted(not_found):
            print("   ", m)

    if not apply:
        print("\nDRY RUN — pass 'apply' to write.")
        return

    b["recipes"] = kept
    b["schemaVersion"] = int(b.get("schemaVersion", 1)) + 1
    with gzip.open(BUNDLE, "wt", encoding="utf-8") as f:
        json.dump(b, f, ensure_ascii=False)
    json.dump(led_kept, io.open(LED, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    json.dump(sorted(del_names), io.open(NAMES_OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"\nWROTE bundle (schemaVersion={b['schemaVersion']}, {len(kept)} recipes)")
    print(f"WROTE ledger ({len(led_kept)} entries)")
    print(f"WROTE {NAMES_OUT.name} ({len(del_names)} names)")

if __name__ == "__main__":
    main()

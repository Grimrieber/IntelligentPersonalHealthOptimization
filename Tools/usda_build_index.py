"""
USDA SR Legacy → lean JSON index builder.

Reads the SR Legacy CSVs in Tools/usda/FoodData_Central_sr_legacy_food_csv_2018-04/
and produces Tools/usda/sr_legacy_index.json — one entry per food with:
  fdc_id, description, category, plus the 9 per-100g macros.

Run once after downloading the SR Legacy zip:
    python Tools/usda_build_index.py
"""

import csv
import json
import sys
from pathlib import Path

HERE = Path(__file__).parent
USDA_DIR = HERE / "usda" / "FoodData_Central_sr_legacy_food_csv_2018-04"
OUT = HERE / "usda" / "sr_legacy_index.json"

# Nutrient IDs from nutrient.csv → our keys
NUTRIENT_MAP = {
    "1008": "cal",           # Energy (KCAL)
    "1003": "protein",       # Protein (G)
    "1004": "fat",           # Total lipid (G)
    "1005": "carb",          # Carbohydrate, by difference (G)
    "1079": "fiber",         # Fiber, total dietary (G)
    "1093": "sodium",        # Sodium, Na (MG)
    "1253": "cholesterol",   # Cholesterol (MG)
    "1258": "satfat",        # Fatty acids, total saturated (G)
    "2000": "sugar",         # Sugars, Total (G)
}


def load_categories():
    cats = {}
    with (USDA_DIR / "food_category.csv").open(newline="", encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            cats[row["id"]] = row["description"]
    return cats


def load_foods(categories):
    foods = {}
    with (USDA_DIR / "food.csv").open(newline="", encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            fid = row["fdc_id"]
            foods[fid] = {
                "fdc_id": int(fid),
                "description": row["description"],
                "category": categories.get(row["food_category_id"], ""),
            }
    return foods


def load_nutrients(foods):
    """Walk food_nutrient.csv once (it's 36MB) and attach the 9 nutrients per food."""
    target_keys = set(NUTRIENT_MAP.keys())
    count = 0
    with (USDA_DIR / "food_nutrient.csv").open(newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        for row in reader:
            nid = row["nutrient_id"]
            if nid not in target_keys:
                continue
            fid = row["fdc_id"]
            food = foods.get(fid)
            if not food:
                continue
            try:
                amt = float(row["amount"])
            except (ValueError, KeyError):
                continue
            food[NUTRIENT_MAP[nid]] = amt
            count += 1
    return count


def main():
    if not USDA_DIR.exists():
        sys.exit(f"USDA dir not found: {USDA_DIR}")
    print(f"Loading from {USDA_DIR}")
    cats = load_categories()
    print(f"  categories: {len(cats)}")
    foods = load_foods(cats)
    print(f"  foods: {len(foods):,}")
    n = load_nutrients(foods)
    print(f"  nutrient values matched: {n:,}")

    # Keep only foods that have at least Energy AND Protein values — those are
    # complete enough to be useful for nutrition computation.
    usable = []
    for f in foods.values():
        if "cal" in f and "protein" in f:
            # Zero-fill missing macros so consumers can rely on the keys existing
            for k in NUTRIENT_MAP.values():
                f.setdefault(k, 0.0)
            usable.append(f)

    print(f"  usable foods (with cal + protein): {len(usable):,}")
    OUT.write_text(json.dumps(usable, separators=(",", ":")), encoding="utf-8")
    size_mb = OUT.stat().st_size / 1024 / 1024
    print(f"\nWrote {OUT}  ({size_mb:.1f} MB)")


if __name__ == "__main__":
    main()

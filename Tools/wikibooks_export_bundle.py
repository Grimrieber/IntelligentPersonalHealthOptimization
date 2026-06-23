"""
Wikibooks Bundle Export
=======================
Reads WIKIBOOKS_* tables from RecipeDB and produces a single gzipped JSON file
ready to be bundled into the app as a MauiAsset.

Output: Resources/Raw/wikibooks_bundle.json.gz
Each recipe is one self-contained object with nested ingredients, directions,
and nutrition. The app seeds these into its on-device SQLite on first launch.

Excludes recipes flagged with IsExcluded = 1.

Usage:
    python Tools/wikibooks_export_bundle.py
"""

import gzip
import json
import os
import sys
from pathlib import Path

import pyodbc

REPO = Path(__file__).parent.parent
OUT_DIR = REPO / "Resources" / "Raw"
OUT_FILE = OUT_DIR / "wikibooks_bundle.json.gz"


def get_conn():
    cs = os.environ.get("RECIPEDB_SQL_CONNECTION", "")
    if not cs:
        sys.exit("RECIPEDB_SQL_CONNECTION env var is not set.")
    return pyodbc.connect(cs)


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    conn = get_conn()
    cur = conn.cursor()

    # ----- categories -----
    cur.execute(
        "SELECT WikibooksCategoryID, CategoryName FROM WIKIBOOKS_Categories "
        "ORDER BY CategoryName"
    )
    cat_by_id = {row[0]: row[1] for row in cur.fetchall()}
    print(f"categories : {len(cat_by_id)}")

    # ----- recipes -----
    cur.execute(
        """
        SELECT WikibooksRecipeID, WikibooksCategoryID, RecipeName,
               PrepTime, CookTime, RestTime, Servings, Difficulty,
               Source, Notes,
               IsVegetarian, IsVegan, IsPescatarian, IsGlutenFree, IsDairyFree,
               IsKeto, IsPaleo, IsHalal, IsKosher, IsMediterranean
        FROM WIKIBOOKS_Recipes
        WHERE IsExcluded = 0
        ORDER BY WikibooksRecipeID
        """
    )
    recipes = {}
    for row in cur.fetchall():
        rid = row[0]
        recipes[rid] = {
            "name": row[2],
            "category": cat_by_id.get(row[1], "Uncategorized"),
            "prepTime": row[3],
            "cookTime": row[4],
            "restTime": row[5],
            "servings": row[6],
            "difficulty": row[7],
            "source": row[8],
            "notes": row[9],
            # Precomputed diet-compatibility flags (see Tools/diet classifier).
            "isVegetarian": bool(row[10]),
            "isVegan": bool(row[11]),
            "isPescatarian": bool(row[12]),
            "isGlutenFree": bool(row[13]),
            "isDairyFree": bool(row[14]),
            "isKeto": bool(row[15]),
            "isPaleo": bool(row[16]),
            "isHalal": bool(row[17]),
            "isKosher": bool(row[18]),
            "isMediterranean": bool(row[19]),
            "ingredients": [],
            "directions": [],
            "nutrition": None,
        }
    print(f"recipes    : {len(recipes):,}")

    # ----- ingredients -----
    cur.execute(
        "SELECT WikibooksRecipeID, SortOrder, IngredientGroup, Description "
        "FROM WIKIBOOKS_RecipeIngredients ORDER BY WikibooksRecipeID, SortOrder"
    )
    ing_count = 0
    for rid, order, grp, desc in cur.fetchall():
        r = recipes.get(rid)
        if r is None:
            continue
        r["ingredients"].append({"order": order, "group": grp, "description": desc})
        ing_count += 1
    print(f"ingredients: {ing_count:,}")

    # ----- directions -----
    cur.execute(
        "SELECT WikibooksRecipeID, StepNumber, DirectionGroup, Instruction "
        "FROM WIKIBOOKS_RecipeDirections ORDER BY WikibooksRecipeID, StepNumber"
    )
    dir_count = 0
    for rid, step, grp, instr in cur.fetchall():
        r = recipes.get(rid)
        if r is None:
            continue
        r["directions"].append({"step": step, "group": grp, "instruction": instr})
        dir_count += 1
    print(f"directions : {dir_count:,}")

    # ----- nutrition -----
    cur.execute(
        """
        SELECT WikibooksRecipeID, CaloriesPerServing, ProteinGrams,
               TotalCarbsGrams, TotalFatGrams, FiberGrams,
               SodiumMg, CholesterolMg, SaturatedFatGrams, SugarGrams,
               ServingSizeNote, IngredientMatchRate
        FROM WIKIBOOKS_RecipeNutrition
        """
    )
    nut_count = 0
    for row in cur.fetchall():
        rid = row[0]
        r = recipes.get(rid)
        if r is None:
            continue

        def _num(x):
            if x is None:
                return None
            # decimal.Decimal → float; int stays int
            return float(x) if not isinstance(x, int) else x

        r["nutrition"] = {
            "caloriesPerServing": row[1],
            "proteinGrams":       _num(row[2]),
            "carbsGrams":         _num(row[3]),
            "fatGrams":           _num(row[4]),
            "fiberGrams":         _num(row[5]),
            "sodiumMg":           _num(row[6]),
            "cholesterolMg":      _num(row[7]),
            "satFatGrams":        _num(row[8]),
            "sugarGrams":         _num(row[9]),
            "servingSizeNote":    row[10],
            "matchRate":          _num(row[11]),
        }
        nut_count += 1
    print(f"nutrition  : {nut_count:,}")

    # Drop recipes without ingredients OR directions (defensive — importer
    # should already have skipped these, but double-check)
    final = [
        r for r in recipes.values()
        if r["ingredients"] or r["directions"]
    ]
    bundle = {
        "schemaVersion": 1,
        "source": "Wikibooks Cookbook (en.wikibooks.org/wiki/Cookbook)",
        "license": "CC-BY-SA 4.0",
        "sourceProvider": "Wikibooks",
        "categories": sorted({r["category"] for r in final}),
        "recipes": final,
    }
    print(f"\nbundling {len(final):,} recipes + {len(bundle['categories'])} categories")

    # Write gzipped to keep the APK small.
    raw = json.dumps(bundle, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    raw_mb = len(raw) / 1024 / 1024
    with gzip.open(OUT_FILE, "wb", compresslevel=9) as f:
        f.write(raw)
    gz_mb = OUT_FILE.stat().st_size / 1024 / 1024
    print(f"\nwrote {OUT_FILE}")
    print(f"  raw JSON : {raw_mb:.2f} MB")
    print(f"  gzipped  : {gz_mb:.2f} MB  ({100*gz_mb/raw_mb:.0f}% of raw)")


if __name__ == "__main__":
    main()

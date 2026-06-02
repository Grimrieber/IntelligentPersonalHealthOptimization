"""Sanity-check the exported wikibooks_bundle.json.gz."""
import gzip
import json
from pathlib import Path

BUNDLE = Path(__file__).parent.parent / "Resources" / "Raw" / "wikibooks_bundle.json.gz"

with gzip.open(BUNDLE, "rb") as f:
    data = json.loads(f.read().decode("utf-8"))

print(f"schemaVersion : {data['schemaVersion']}")
print(f"source        : {data['source']}")
print(f"license       : {data['license']}")
print(f"categories    : {len(data['categories'])}")
print(f"recipes       : {len(data['recipes']):,}")

# Sample a 100%-match recipe
sample = next(
    r for r in data["recipes"]
    if r.get("nutrition") and r["nutrition"].get("matchRate") == 100.0
       and r["servings"]
)
print("\nSample recipe:")
print(f"  name      : {sample['name']}")
print(f"  category  : {sample['category']}")
print(f"  servings  : {sample['servings']}")
print(f"  cookTime  : {sample['cookTime']}")
print(f"  source    : {sample['source']}")
print(f"  ingredients: {len(sample['ingredients'])} lines")
print(f"  directions : {len(sample['directions'])} steps")
n = sample['nutrition']
print(f"  nutrition  : {n['caloriesPerServing']} cal | "
      f"P:{n['proteinGrams']}g C:{n['carbsGrams']}g F:{n['fatGrams']}g | "
      f"Na:{n['sodiumMg']}mg Chol:{n['cholesterolMg']}mg "
      f"SatFat:{n['satFatGrams']}g Sug:{n['sugarGrams']}g")
print(f"  note       : {n['servingSizeNote']}  ({n['matchRate']}% match)")

# Aggregate counts of nutrition completeness
have_nut = sum(1 for r in data["recipes"] if r.get("nutrition"))
no_nut   = sum(1 for r in data["recipes"] if not r.get("nutrition"))
per_serv = sum(1 for r in data["recipes"] if r.get("nutrition") and "Per serving" in (r["nutrition"]["servingSizeNote"] or ""))
total    = sum(1 for r in data["recipes"] if r.get("nutrition") and "Total recipe" in (r["nutrition"]["servingSizeNote"] or ""))
print(f"\nNutrition coverage:")
print(f"  with nutrition row    : {have_nut:,}")
print(f"  without nutrition row : {no_nut:,}")
print(f"    per-serving values  : {per_serv:,}")
print(f"    total-recipe values : {total:,}")

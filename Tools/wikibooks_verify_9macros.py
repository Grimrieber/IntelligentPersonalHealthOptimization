"""Verify the 4 newly-computed macros are populated."""
import os
import pyodbc

c = pyodbc.connect(os.environ["RECIPEDB_SQL_CONNECTION"]).cursor()

# Coverage of each macro
print("Coverage by macro field (rows with non-null + non-zero value):")
for col in ["CaloriesPerServing", "ProteinGrams", "TotalCarbsGrams",
            "TotalFatGrams", "FiberGrams", "SodiumMg", "CholesterolMg",
            "SaturatedFatGrams", "SugarGrams"]:
    c.execute(f"SELECT COUNT(*) FROM WIKIBOOKS_RecipeNutrition WHERE {col} IS NOT NULL AND {col} > 0")
    print(f"  {col:<22} {c.fetchone()[0]:>6,}")

# Sample 3 high-match recipes with all 9 fields
print("\n3 sample recipes (all 9 macros, per-serving):")
c.execute("""
    SELECT TOP 3 r.RecipeName, r.Servings,
           n.CaloriesPerServing, n.ProteinGrams, n.TotalCarbsGrams, n.TotalFatGrams,
           n.FiberGrams, n.SodiumMg, n.CholesterolMg, n.SaturatedFatGrams, n.SugarGrams,
           n.IngredientMatchRate
    FROM WIKIBOOKS_Recipes r
    JOIN WIKIBOOKS_RecipeNutrition n ON n.WikibooksRecipeID = r.WikibooksRecipeID
    WHERE n.IngredientMatchRate = 100
      AND n.ServingSizeNote LIKE 'Per serving%'
      AND r.Servings IS NOT NULL
      AND n.SodiumMg > 0
    ORDER BY r.RecipeName
""")
for row in c.fetchall():
    name, srv, cal, p, ca, f, fi, na, ch, sf, sg, mr = row
    name = (name[:38] + "...") if len(name) > 38 else name
    print(f"\n  {name}  (servings={srv}, match={mr}%)")
    print(f"    cal={cal}  protein={p}g  carbs={ca}g  fat={f}g  fiber={fi}g")
    print(f"    sodium={na}mg  cholesterol={ch}mg  satfat={sf}g  sugar={sg}g")

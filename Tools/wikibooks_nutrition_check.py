"""Spot-check WIKIBOOKS_RecipeNutrition after recompute."""
import os
import pyodbc

c = pyodbc.connect(os.environ["RECIPEDB_SQL_CONNECTION"]).cursor()

# Aggregate stats
c.execute("SELECT COUNT(*) FROM WIKIBOOKS_RecipeNutrition")
print(f"Nutrition rows: {c.fetchone()[0]:,}")

c.execute(
    "SELECT COUNT(*) FROM WIKIBOOKS_RecipeNutrition WHERE ServingSizeNote LIKE 'Per serving%'"
)
per_serv = c.fetchone()[0]
c.execute(
    "SELECT COUNT(*) FROM WIKIBOOKS_RecipeNutrition WHERE ServingSizeNote LIKE 'Total recipe%'"
)
totals = c.fetchone()[0]
print(f"  per-serving values: {per_serv:,}")
print(f"  total-recipe values (servings unknown): {totals:,}")

c.execute(
    "SELECT AVG(IngredientMatchRate) FROM WIKIBOOKS_RecipeNutrition"
)
print(f"\nMean IngredientMatchRate: {c.fetchone()[0]:.1f}%")

# Sample 5 high-match recipes
print("\n5 high-match recipes (per-serving):")
c.execute(
    """
    SELECT TOP 5 r.RecipeName, r.Servings, n.CaloriesPerServing, n.ProteinGrams,
                 n.TotalCarbsGrams, n.TotalFatGrams, n.IngredientMatchRate, n.ServingSizeNote
    FROM WIKIBOOKS_Recipes r
    JOIN WIKIBOOKS_RecipeNutrition n ON n.WikibooksRecipeID = r.WikibooksRecipeID
    WHERE n.IngredientMatchRate >= 90
      AND n.ServingSizeNote LIKE 'Per serving%'
      AND r.Servings IS NOT NULL
    ORDER BY n.IngredientMatchRate DESC, r.RecipeName
    """
)
for name, srv, cal, p, c2, f, mr, note in c.fetchall():
    name = (name[:40] + "...") if len(name) > 40 else name
    print(f"  {name:<43} srv={srv:<6} cal={cal:<5} P/C/F={p}/{c2}/{f}  match={mr}%")

print("\n5 low-match recipes (audit candidates):")
c.execute(
    """
    SELECT TOP 5 r.RecipeName, n.IngredientMatchRate
    FROM WIKIBOOKS_Recipes r
    JOIN WIKIBOOKS_RecipeNutrition n ON n.WikibooksRecipeID = r.WikibooksRecipeID
    WHERE n.IngredientMatchRate < 30
    ORDER BY n.IngredientMatchRate ASC
    """
)
for name, mr in c.fetchall():
    name = (name[:60] + "...") if len(name) > 60 else name
    print(f"  match={mr}%  {name}")

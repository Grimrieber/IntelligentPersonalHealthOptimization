"""Check the Almond Cake specifically — that was the 7,028 cal/serving outlier."""
import os
import pyodbc

c = pyodbc.connect(os.environ["RECIPEDB_SQL_CONNECTION"]).cursor()

c.execute(
    """
    SELECT r.WikibooksRecipeID, r.RecipeName, r.Servings, r.Source,
           n.CaloriesPerServing, n.ProteinGrams, n.TotalCarbsGrams,
           n.TotalFatGrams, n.FiberGrams, n.IngredientMatchRate, n.ServingSizeNote
    FROM WIKIBOOKS_Recipes r
    LEFT JOIN WIKIBOOKS_RecipeNutrition n ON n.WikibooksRecipeID = r.WikibooksRecipeID
    WHERE r.RecipeName = 'Almond Cake'
    """
)
for row in c.fetchall():
    print(f"[{row[0]}] {row[1]}")
    print(f"  servings={row[2]!r}  match={row[9]}%")
    print(f"  note: {row[10]}")
    print(f"  calories: {row[4]}  P/C/F/Fiber: {row[5]}/{row[6]}/{row[7]}/{row[8]}")
    rid = row[0]

c.execute(
    "SELECT SortOrder, Description FROM WIKIBOOKS_RecipeIngredients "
    "WHERE WikibooksRecipeID=? ORDER BY SortOrder", rid
)
print("\n  Ingredients:")
for so, d in c.fetchall():
    print(f"    {so}. {d}")

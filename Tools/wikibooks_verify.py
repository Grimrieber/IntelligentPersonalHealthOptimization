"""Quick verification of WIKIBOOKS_* table contents after import."""
import os
import pyodbc

conn = pyodbc.connect(os.environ["RECIPEDB_SQL_CONNECTION"])
cur = conn.cursor()

print("Row counts:")
for t in [
    "WIKIBOOKS_Categories",
    "WIKIBOOKS_Recipes",
    "WIKIBOOKS_RecipeIngredients",
    "WIKIBOOKS_RecipeDirections",
    "WIKIBOOKS_RecipeNutrition",
]:
    cur.execute(f"SELECT COUNT(*) FROM {t}")
    n = cur.fetchone()[0]
    print(f"  {t:<32} {n:>8,}")

print("\nTop 10 categories by recipe count:")
cur.execute(
    """
    SELECT TOP 10 c.CategoryName, COUNT(*) AS n
    FROM WIKIBOOKS_Recipes r
    JOIN WIKIBOOKS_Categories c ON c.WikibooksCategoryID = r.WikibooksCategoryID
    GROUP BY c.CategoryName ORDER BY n DESC
    """
)
for name, n in cur.fetchall():
    print(f"  {n:>4}  {name}")

print("\nIngredient / direction averages:")
cur.execute(
    """
    SELECT AVG(CAST(ic AS FLOAT)) FROM (
        SELECT COUNT(*) AS ic FROM WIKIBOOKS_RecipeIngredients GROUP BY WikibooksRecipeID
    ) t
    """
)
print(f"  avg ingredients/recipe : {cur.fetchone()[0]:.1f}")
cur.execute(
    """
    SELECT AVG(CAST(dc AS FLOAT)) FROM (
        SELECT COUNT(*) AS dc FROM WIKIBOOKS_RecipeDirections GROUP BY WikibooksRecipeID
    ) t
    """
)
print(f"  avg directions/recipe  : {cur.fetchone()[0]:.1f}")

print("\nSample recipe:")
cur.execute(
    """
    SELECT TOP 1 WikibooksRecipeID, RecipeName, Servings, CookTime, Source
    FROM WIKIBOOKS_Recipes
    WHERE Servings IS NOT NULL AND CookTime IS NOT NULL
    ORDER BY WikibooksRecipeID
    """
)
rid, name, srv, ct, src = cur.fetchone()
print(f"  [{rid}] {name}")
print(f"    servings={srv}  cook={ct}")
print(f"    source: {src}")

cur.execute(
    "SELECT TOP 5 SortOrder, Description FROM WIKIBOOKS_RecipeIngredients "
    "WHERE WikibooksRecipeID=? ORDER BY SortOrder",
    rid,
)
print("  ingredients:")
for so, d in cur.fetchall():
    print(f"    {so}. {d}")

cur.execute(
    "SELECT TOP 5 StepNumber, Instruction FROM WIKIBOOKS_RecipeDirections "
    "WHERE WikibooksRecipeID=? ORDER BY StepNumber",
    rid,
)
print("  directions:")
for sn, ins in cur.fetchall():
    txt = ins[:120] + ("..." if len(ins) > 120 else "")
    print(f"    {sn}. {txt}")

"""One-off: sample the Servings strings to inform parser improvements."""
import os
import pyodbc

c = pyodbc.connect(os.environ["RECIPEDB_SQL_CONNECTION"]).cursor()

c.execute("SELECT COUNT(*) FROM WIKIBOOKS_Recipes WHERE Servings IS NULL")
print("NULL servings rows:", c.fetchone()[0])

c.execute("SELECT COUNT(*) FROM WIKIBOOKS_Recipes WHERE Servings IS NOT NULL")
print("Non-null servings rows:", c.fetchone()[0])

c.execute(
    "SELECT TOP 30 Servings, COUNT(*) AS n FROM WIKIBOOKS_Recipes "
    "WHERE Servings IS NOT NULL GROUP BY Servings ORDER BY n DESC"
)
print("\nTop 30 non-null Servings values:")
for s, n in c.fetchall():
    print(f"  {n:>4}  {s!r}")

# How many of the non-null ones DO parse with the current logic?
import re
def parse_v1(s):
    if not s: return None
    t = s.strip().lower()
    m = re.search(r"(\d+)\s*(?:-|–|to)\s*(\d+)", t)
    if m: return (float(m.group(1)) + float(m.group(2))) / 2.0
    m = re.search(r"(\d+(?:\.\d+)?)", t)
    if m: return float(m.group(1))
    return None

c.execute("SELECT Servings FROM WIKIBOOKS_Recipes WHERE Servings IS NOT NULL")
all_non_null = [r[0] for r in c.fetchall()]
parsed_ok = sum(1 for s in all_non_null if parse_v1(s) is not None)
print(f"\nNon-null servings that parse_v1 successfully extracts: {parsed_ok} / {len(all_non_null)}")

# Show 20 examples that FAIL to parse
fails = [s for s in all_non_null if parse_v1(s) is None][:20]
print(f"\nFirst 20 non-null values that v1 parser cannot extract:")
for s in fails:
    print(f"  {s!r}")

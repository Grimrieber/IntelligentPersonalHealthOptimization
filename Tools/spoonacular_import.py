"""
Spoonacular Recipe Import Tool
===============================
Fetches recipes from the Spoonacular API and inserts them into staging tables
in the RecipeDB SQL Server database for review before promoting to production tables.

Usage:
    1. Get a free API key from https://spoonacular.com/food-api
    2. Set your API key below or as environment variable SPOONACULAR_API_KEY
    3. Run: python spoonacular_import.py
    4. Review data in Staging_* tables in RecipeDB
    5. Run the promote query at the bottom to move approved recipes to production

Free tier: ~150 requests/day. Each recipe detail = 1 request.
This script fetches in batches with delays to stay within limits.
"""

import os
import sys
import time
import json
import requests
import pyodbc

# ===== CONFIGURATION =====
# Set both env vars before running:
#   SPOONACULAR_API_KEY=...
#   RECIPEDB_SQL_CONNECTION="DRIVER={ODBC Driver 17 for SQL Server};SERVER=localhost;DATABASE=RecipeDB;UID=sa;PWD=...;TrustServerCertificate=yes;"
API_KEY = os.environ.get("SPOONACULAR_API_KEY", "")
BASE_URL = "https://api.spoonacular.com"

SQL_CONNECTION = os.environ.get("RECIPEDB_SQL_CONNECTION", "")

# How many recipes to fetch per search query (max 100 per API call)
RECIPES_PER_QUERY = 100

# How many pages to fetch per query (offset by RECIPES_PER_QUERY each time)
PAGES_PER_QUERY = 3  # 3 pages x 100 = up to 300 per query

# Search queries to cover different meal types and cuisines
SEARCH_QUERIES = [
    # Breakfast
    ("breakfast", "Breakfast"),
    ("oatmeal porridge", "Breakfast"),
    ("eggs breakfast", "Breakfast"),
    ("pancakes", "Breakfast"),
    ("smoothie", "Breakfast"),
    ("french toast", "Breakfast"),
    ("muffin breakfast", "Breakfast"),
    # Lunch
    ("chicken salad", "Main Dishes"),
    ("sandwich", "Main Dishes"),
    ("wrap", "Main Dishes"),
    ("soup", "Soups"),
    ("grain bowl", "Main Dishes"),
    ("burrito", "Main Dishes"),
    ("quesadilla", "Main Dishes"),
    # Dinner - Chicken
    ("chicken breast", "Main Dishes"),
    ("chicken thigh", "Main Dishes"),
    ("chicken baked", "Main Dishes"),
    ("chicken grilled", "Main Dishes"),
    # Dinner - Beef
    ("beef steak", "Main Dishes"),
    ("ground beef", "Main Dishes"),
    ("beef roast", "Main Dishes"),
    ("beef stew", "Main Dishes"),
    # Dinner - Pork
    ("pork chop", "Main Dishes"),
    ("pork tenderloin", "Main Dishes"),
    ("pulled pork", "Main Dishes"),
    # Dinner - Turkey
    ("turkey", "Main Dishes"),
    ("turkey meatball", "Main Dishes"),
    # Dinner - Fish/Seafood
    ("salmon", "Seafood"),
    ("shrimp", "Seafood"),
    ("fish fillet", "Seafood"),
    ("tuna", "Seafood"),
    ("cod tilapia", "Seafood"),
    # Pasta
    ("pasta", "Pasta"),
    ("spaghetti", "Pasta"),
    ("lasagna", "Pasta"),
    ("mac and cheese", "Pasta"),
    # Casseroles
    ("casserole", "Casseroles"),
    # Stir Fry / Asian
    ("stir fry", "Main Dishes"),
    ("curry", "Main Dishes"),
    ("teriyaki", "Main Dishes"),
    ("fried rice", "Main Dishes"),
    # Mexican
    ("taco", "Main Dishes"),
    ("enchilada", "Main Dishes"),
    ("fajita", "Main Dishes"),
    # Italian
    ("italian chicken", "Main Dishes"),
    ("risotto", "Main Dishes"),
    # Diet specific
    ("high protein", "Main Dishes"),
    ("low carb", "Main Dishes"),
    ("keto", "Main Dishes"),
    ("mediterranean", "Main Dishes"),
    ("whole30", "Main Dishes"),
    ("paleo", "Main Dishes"),
    # Vegetarian / Vegan
    ("vegetarian", "Vegetarian"),
    ("vegan dinner", "Vegan"),
    ("vegan lunch", "Vegan"),
    ("tofu", "Vegan"),
    ("lentil", "Vegetarian"),
    ("chickpea", "Vegetarian"),
    # Snacks
    ("healthy snack", "Snacks"),
    ("protein snack", "Snacks"),
    ("energy bites", "Snacks"),
    ("hummus", "Snacks"),
    ("trail mix", "Snacks"),
    # Sides
    ("side dish vegetable", "Side Dishes"),
    ("roasted vegetables", "Side Dishes"),
    ("steamed vegetables", "Side Dishes"),
    ("rice pilaf", "Side Dishes"),
    # Salads
    ("salad", "Salads"),
    ("quinoa salad", "Salads"),
    ("greek salad", "Salads"),
    # Slow cooker
    ("slow cooker", "Main Dishes"),
    ("crockpot", "Main Dishes"),
    ("instant pot", "Main Dishes"),
    # Meal prep
    ("meal prep", "Main Dishes"),
    ("sheet pan", "Main Dishes"),
    ("one pot", "Main Dishes"),
]


def create_staging_tables(cursor):
    """Create staging tables that mirror the production schema."""

    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_Recipes')
    CREATE TABLE Staging_Recipes (
        StagingID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularID INT NOT NULL UNIQUE,
        RecipeName NVARCHAR(500) NOT NULL,
        CategoryName NVARCHAR(100) DEFAULT 'Uncategorized',
        PrepTime NVARCHAR(50),
        CookTime NVARCHAR(50),
        Servings NVARCHAR(50),
        Difficulty NVARCHAR(20),
        Source NVARCHAR(500),
        SourceUrl NVARCHAR(1000),
        Notes NVARCHAR(MAX),
        ImageUrl NVARCHAR(1000),
        DietLabels NVARCHAR(500),
        DishTypes NVARCHAR(500),
        Cuisines NVARCHAR(500),
        IsApproved BIT DEFAULT 0,
        ImportedAt DATETIME DEFAULT GETDATE()
    )
    """)

    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_RecipeIngredients')
    CREATE TABLE Staging_RecipeIngredients (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularRecipeID INT NOT NULL,
        SortOrder INT DEFAULT 0,
        IngredientGroup NVARCHAR(100),
        Description NVARCHAR(500) NOT NULL,
        OriginalName NVARCHAR(200),
        Amount DECIMAL(10,2),
        Unit NVARCHAR(50),
        AisleName NVARCHAR(100)
    )
    """)

    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_RecipeDirections')
    CREATE TABLE Staging_RecipeDirections (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularRecipeID INT NOT NULL,
        StepNumber INT NOT NULL,
        DirectionGroup NVARCHAR(100),
        Instruction NVARCHAR(MAX) NOT NULL
    )
    """)

    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_RecipeNutrition')
    CREATE TABLE Staging_RecipeNutrition (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularRecipeID INT NOT NULL UNIQUE,
        CaloriesPerServing INT,
        TotalFatGrams DECIMAL(10,2),
        SaturatedFatGrams DECIMAL(10,2),
        CholesterolMg DECIMAL(10,2),
        SodiumMg DECIMAL(10,2),
        TotalCarbsGrams DECIMAL(10,2),
        FiberGrams DECIMAL(10,2),
        SugarGrams DECIMAL(10,2),
        ProteinGrams DECIMAL(10,2),
        ServingSizeNote NVARCHAR(200)
    )
    """)

    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_RecipeTags')
    CREATE TABLE Staging_RecipeTags (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularRecipeID INT NOT NULL,
        TagName NVARCHAR(100) NOT NULL
    )
    """)

    cursor.connection.commit()
    print("Staging tables created/verified.")


def search_recipes(query, number=100, offset=0):
    """Search Spoonacular for recipes."""
    url = f"{BASE_URL}/recipes/complexSearch"
    params = {
        "apiKey": API_KEY,
        "query": query,
        "number": number,
        "offset": offset,
        "addRecipeNutrition": True,
        "addRecipeInformation": True,
        "fillIngredients": True,
        "instructionsRequired": True,
    }

    resp = requests.get(url, params=params, timeout=30)
    if resp.status_code == 402:
        print("  API quota exceeded! Wait until tomorrow or upgrade your plan.")
        return []
    resp.raise_for_status()
    data = resp.json()
    return data.get("results", [])


def get_recipe_instructions(recipe_id):
    """Get analyzed instructions for a recipe."""
    url = f"{BASE_URL}/recipes/{recipe_id}/analyzedInstructions"
    params = {"apiKey": API_KEY}

    resp = requests.get(url, params=params, timeout=30)
    if resp.status_code == 402:
        return []
    resp.raise_for_status()
    return resp.json()


def extract_nutrient(nutrients, name):
    """Extract a nutrient value from the Spoonacular nutrients array."""
    for n in nutrients:
        if n.get("name", "").lower() == name.lower():
            return n.get("amount")
    return None


def insert_recipe(cursor, recipe, category):
    """Insert a single recipe into staging tables."""
    spoon_id = recipe["id"]

    # Check if already imported
    cursor.execute("SELECT COUNT(*) FROM Staging_Recipes WHERE SpoonacularID = ?", spoon_id)
    if cursor.fetchone()[0] > 0:
        return False  # Skip duplicate

    title = recipe.get("title", "Unknown")
    prep_min = recipe.get("preparationMinutes") or -1
    cook_min = recipe.get("cookingMinutes") or -1
    ready_min = recipe.get("readyInMinutes") or 0
    servings = recipe.get("servings", 1)
    source = recipe.get("sourceName", "")
    source_url = recipe.get("sourceUrl", "")
    summary = recipe.get("summary", "")
    image = recipe.get("image", "")

    # Clean HTML from summary
    import re
    summary = re.sub(r'<[^>]+>', '', summary) if summary else ""
    if len(summary) > 2000:
        summary = summary[:2000] + "..."

    # Diet labels
    diets = recipe.get("diets", [])
    dish_types = recipe.get("dishTypes", [])
    cuisines = recipe.get("cuisines", [])

    prep_time = f"{prep_min} min" if prep_min > 0 else (f"{ready_min} min" if ready_min > 0 else None)
    cook_time = f"{cook_min} min" if cook_min > 0 else None

    # Insert recipe
    cursor.execute("""
        INSERT INTO Staging_Recipes
        (SpoonacularID, RecipeName, CategoryName, PrepTime, CookTime, Servings,
         Source, SourceUrl, Notes, ImageUrl, DietLabels, DishTypes, Cuisines)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """, spoon_id, title, category, prep_time, cook_time, str(servings),
         source, source_url, summary, image,
         ", ".join(diets), ", ".join(dish_types), ", ".join(cuisines))

    # Insert ingredients
    ingredients = recipe.get("extendedIngredients", [])
    for i, ing in enumerate(ingredients):
        desc = ing.get("original", ing.get("name", "Unknown"))
        name = ing.get("name", "")
        amount = ing.get("amount", 0)
        unit = ing.get("unit", "")
        aisle = ing.get("aisle", "")

        cursor.execute("""
            INSERT INTO Staging_RecipeIngredients
            (SpoonacularRecipeID, SortOrder, Description, OriginalName, Amount, Unit, AisleName)
            VALUES (?, ?, ?, ?, ?, ?, ?)
        """, spoon_id, i + 1, desc, name, amount, unit, aisle)

    # Insert nutrition
    nutrition = recipe.get("nutrition", {})
    nutrients = nutrition.get("nutrients", [])
    if nutrients:
        calories = extract_nutrient(nutrients, "Calories")
        fat = extract_nutrient(nutrients, "Fat")
        sat_fat = extract_nutrient(nutrients, "Saturated Fat")
        cholesterol = extract_nutrient(nutrients, "Cholesterol")
        sodium = extract_nutrient(nutrients, "Sodium")
        carbs = extract_nutrient(nutrients, "Carbohydrates")
        fiber = extract_nutrient(nutrients, "Fiber")
        sugar = extract_nutrient(nutrients, "Sugar")
        protein = extract_nutrient(nutrients, "Protein")

        cursor.execute("""
            INSERT INTO Staging_RecipeNutrition
            (SpoonacularRecipeID, CaloriesPerServing, TotalFatGrams, SaturatedFatGrams,
             CholesterolMg, SodiumMg, TotalCarbsGrams, FiberGrams, SugarGrams,
             ProteinGrams, ServingSizeNote)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """, spoon_id,
             int(calories) if calories else None,
             fat, sat_fat, cholesterol, sodium, carbs, fiber, sugar, protein,
             f"{servings} serving(s)")

    # Insert directions from analyzed instructions
    instructions = recipe.get("analyzedInstructions", [])
    step_num = 0
    for instruction_group in instructions:
        group_name = instruction_group.get("name", "")
        for step in instruction_group.get("steps", []):
            step_num += 1
            cursor.execute("""
                INSERT INTO Staging_RecipeDirections
                (SpoonacularRecipeID, StepNumber, DirectionGroup, Instruction)
                VALUES (?, ?, ?, ?)
            """, spoon_id, step_num,
                 group_name if group_name else None,
                 step.get("step", ""))

    # Insert tags (from diets, dish types, cuisines)
    all_tags = set()
    all_tags.update(diets)
    all_tags.update(dish_types)
    all_tags.update(cuisines)

    for tag in all_tags:
        if tag:
            cursor.execute("""
                INSERT INTO Staging_RecipeTags (SpoonacularRecipeID, TagName)
                VALUES (?, ?)
            """, spoon_id, tag.strip())

    return True


def main():
    if not API_KEY:
        print("=" * 60)
        print("Missing SPOONACULAR_API_KEY environment variable.")
        print("Get a free key at: https://spoonacular.com/food-api")
        print("Set it via: setx SPOONACULAR_API_KEY <your-key>  (then reopen shell)")
        print("=" * 60)
        sys.exit(1)

    if not SQL_CONNECTION:
        print("=" * 60)
        print("Missing RECIPEDB_SQL_CONNECTION environment variable.")
        print("Example: setx RECIPEDB_SQL_CONNECTION \"DRIVER={ODBC Driver 17 for SQL Server};SERVER=localhost;DATABASE=RecipeDB;UID=sa;PWD=<password>;TrustServerCertificate=yes;\"")
        print("=" * 60)
        sys.exit(1)

    print("Connecting to RecipeDB...")
    conn = pyodbc.connect(SQL_CONNECTION)
    cursor = conn.cursor()

    create_staging_tables(cursor)

    total_imported = 0
    total_skipped = 0

    quota_exceeded = False

    for query, category in SEARCH_QUERIES:
        if quota_exceeded:
            break

        for page in range(PAGES_PER_QUERY):
            offset = page * RECIPES_PER_QUERY
            print(f"\nSearching: '{query}' -> {category} (page {page + 1}, offset {offset})")

            try:
                recipes = search_recipes(query, RECIPES_PER_QUERY, offset)
            except requests.exceptions.HTTPError as e:
                if e.response and e.response.status_code == 402:
                    print("  API quota exceeded! Run again tomorrow for more.")
                    quota_exceeded = True
                    break
                print(f"  Error: {e}")
                break
            except Exception as e:
                print(f"  Error searching: {e}")
                break

            if not recipes:
                if page == 0:
                    print("  No results.")
                break

            page_imported = 0
            for recipe in recipes:
                try:
                    inserted = insert_recipe(cursor, recipe, category)
                    if inserted:
                        total_imported += 1
                        page_imported += 1
                    else:
                        total_skipped += 1
                except Exception:
                    total_skipped += 1
                    continue

            conn.commit()
            print(f"  Imported {page_imported} new ({len(recipes)} returned, {total_skipped} total skipped)")

            # If fewer results than requested, no more pages
            if len(recipes) < RECIPES_PER_QUERY:
                break

            time.sleep(1.5)

    conn.commit()

    # Print summary
    cursor.execute("SELECT COUNT(*) FROM Staging_Recipes")
    total_in_db = cursor.fetchone()[0]

    cursor.execute("""
        SELECT COUNT(*) FROM Staging_Recipes r
        WHERE EXISTS (SELECT 1 FROM Staging_RecipeNutrition n WHERE n.SpoonacularRecipeID = r.SpoonacularID)
    """)
    with_nutrition = cursor.fetchone()[0]

    cursor.execute("""
        SELECT COUNT(*) FROM Staging_Recipes r
        WHERE EXISTS (SELECT 1 FROM Staging_RecipeDirections d WHERE d.SpoonacularRecipeID = r.SpoonacularID)
    """)
    with_directions = cursor.fetchone()[0]

    print(f"\n{'=' * 60}")
    print(f"Import Complete!")
    print(f"  New recipes imported: {total_imported}")
    print(f"  Duplicates skipped:  {total_skipped}")
    print(f"  Total in staging:    {total_in_db}")
    print(f"  With nutrition data: {with_nutrition}")
    print(f"  With directions:     {with_directions}")
    print(f"{'=' * 60}")

    print(f"\nNext steps:")
    print(f"  1. Review recipes in Staging_Recipes table")
    print(f"  2. Set IsApproved = 1 for recipes you want to keep")
    print(f"  3. Run the promote script below to move to production tables")

    cursor.close()
    conn.close()


# ===== PROMOTE APPROVED RECIPES TO PRODUCTION =====
PROMOTE_SQL = """
-- Run this after reviewing and approving recipes in Staging_Recipes
-- (Set IsApproved = 1 on the ones you want to keep)

-- 1. Ensure categories exist
INSERT INTO Categories (CategoryName)
SELECT DISTINCT s.CategoryName
FROM Staging_Recipes s
WHERE s.IsApproved = 1
  AND s.CategoryName NOT IN (SELECT CategoryName FROM Categories);

-- 2. Insert recipes
INSERT INTO Recipes (CategoryID, RecipeName, PrepTime, CookTime, Servings, Source, Notes, DateAdded)
SELECT c.CategoryID, s.RecipeName, s.PrepTime, s.CookTime, s.Servings, s.Source, s.Notes, GETDATE()
FROM Staging_Recipes s
JOIN Categories c ON s.CategoryName = c.CategoryName
WHERE s.IsApproved = 1
  AND s.SpoonacularID NOT IN (
      SELECT CAST(Source AS INT) FROM Recipes WHERE Source IS NOT NULL AND ISNUMERIC(Source) = 1
  );

-- 3. Insert ingredients (match by recipe name since IDs differ)
INSERT INTO RecipeIngredients (RecipeID, SortOrder, IngredientGroup, Description)
SELECT r.RecipeID, si.SortOrder, si.IngredientGroup, si.Description
FROM Staging_RecipeIngredients si
JOIN Staging_Recipes sr ON si.SpoonacularRecipeID = sr.SpoonacularID
JOIN Recipes r ON r.RecipeName = sr.RecipeName
WHERE sr.IsApproved = 1
  AND NOT EXISTS (SELECT 1 FROM RecipeIngredients ri WHERE ri.RecipeID = r.RecipeID);

-- 4. Insert directions
INSERT INTO RecipeDirections (RecipeID, StepNumber, DirectionGroup, Instruction)
SELECT r.RecipeID, sd.StepNumber, sd.DirectionGroup, sd.Instruction
FROM Staging_RecipeDirections sd
JOIN Staging_Recipes sr ON sd.SpoonacularRecipeID = sr.SpoonacularID
JOIN Recipes r ON r.RecipeName = sr.RecipeName
WHERE sr.IsApproved = 1
  AND NOT EXISTS (SELECT 1 FROM RecipeDirections rd WHERE rd.RecipeID = r.RecipeID);

-- 5. Insert nutrition
INSERT INTO RecipeNutrition (RecipeID, CaloriesPerServing, TotalFatGrams, SaturatedFatGrams,
    CholesterolMg, SodiumMg, TotalCarbsGrams, FiberGrams, SugarGrams, ProteinGrams, ServingSizeNote)
SELECT r.RecipeID, sn.CaloriesPerServing, sn.TotalFatGrams, sn.SaturatedFatGrams,
    sn.CholesterolMg, sn.SodiumMg, sn.TotalCarbsGrams, sn.FiberGrams, sn.SugarGrams,
    sn.ProteinGrams, sn.ServingSizeNote
FROM Staging_RecipeNutrition sn
JOIN Staging_Recipes sr ON sn.SpoonacularRecipeID = sr.SpoonacularID
JOIN Recipes r ON r.RecipeName = sr.RecipeName
WHERE sr.IsApproved = 1
  AND NOT EXISTS (SELECT 1 FROM RecipeNutrition rn WHERE rn.RecipeID = r.RecipeID);

-- 6. Insert tags
INSERT INTO RecipeTags (RecipeID, TagName)
SELECT r.RecipeID, st.TagName
FROM Staging_RecipeTags st
JOIN Staging_Recipes sr ON st.SpoonacularRecipeID = sr.SpoonacularID
JOIN Recipes r ON r.RecipeName = sr.RecipeName
WHERE sr.IsApproved = 1
  AND NOT EXISTS (SELECT 1 FROM RecipeTags rt WHERE rt.RecipeID = r.RecipeID AND rt.TagName = st.TagName);

PRINT 'Promotion complete!';
"""


if __name__ == "__main__":
    main()

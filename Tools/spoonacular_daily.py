"""
Spoonacular Daily Recipe Import
================================
Runs daily via Windows Task Scheduler.
- Days 1-5: Uses keyword search queries to build the core library
- Days 6+: Uses random recipes + nutrient-based search to continuously discover new recipes

Logs results to spoonacular_import.log
"""

import os
import sys
import time
import json
import random
import logging
import requests
import pyodbc
from datetime import datetime

# ===== CONFIGURATION =====
# Set env vars before running: SPOONACULAR_API_KEY and RECIPEDB_SQL_CONNECTION
API_KEY = os.environ.get("SPOONACULAR_API_KEY", "")
BASE_URL = "https://api.spoonacular.com"

SQL_CONNECTION = os.environ.get("RECIPEDB_SQL_CONNECTION", "")

if not API_KEY or not SQL_CONNECTION:
    raise SystemExit("Missing SPOONACULAR_API_KEY or RECIPEDB_SQL_CONNECTION environment variable.")

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
LOG_FILE = os.path.join(SCRIPT_DIR, "spoonacular_import.log")
STATE_FILE = os.path.join(SCRIPT_DIR, "import_state.json")

RECIPES_PER_QUERY = 100
PAGES_PER_QUERY = 3

# ===== LOGGING =====
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[
        logging.FileHandler(LOG_FILE, encoding="utf-8"),
        logging.StreamHandler()
    ]
)
log = logging.getLogger(__name__)

# ===== SEARCH QUERIES (Days 1-5) =====
KEYWORD_QUERIES = [
    ("breakfast", "Breakfast"), ("oatmeal porridge", "Breakfast"),
    ("eggs breakfast", "Breakfast"), ("pancakes", "Breakfast"),
    ("smoothie", "Breakfast"), ("french toast", "Breakfast"),
    ("muffin breakfast", "Breakfast"), ("granola", "Breakfast"),
    ("chicken salad", "Main Dishes"), ("sandwich", "Main Dishes"),
    ("wrap", "Main Dishes"), ("soup", "Soups"),
    ("grain bowl", "Main Dishes"), ("burrito", "Main Dishes"),
    ("quesadilla", "Main Dishes"), ("chicken breast", "Main Dishes"),
    ("chicken thigh", "Main Dishes"), ("chicken baked", "Main Dishes"),
    ("chicken grilled", "Main Dishes"), ("beef steak", "Main Dishes"),
    ("ground beef", "Main Dishes"), ("beef roast", "Main Dishes"),
    ("beef stew", "Main Dishes"), ("pork chop", "Main Dishes"),
    ("pork tenderloin", "Main Dishes"), ("pulled pork", "Main Dishes"),
    ("turkey", "Main Dishes"), ("turkey meatball", "Main Dishes"),
    ("salmon", "Seafood"), ("shrimp", "Seafood"),
    ("fish fillet", "Seafood"), ("tuna", "Seafood"),
    ("cod tilapia", "Seafood"), ("pasta", "Pasta"),
    ("spaghetti", "Pasta"), ("lasagna", "Pasta"),
    ("mac and cheese", "Pasta"), ("casserole", "Casseroles"),
    ("stir fry", "Main Dishes"), ("curry", "Main Dishes"),
    ("teriyaki", "Main Dishes"), ("fried rice", "Main Dishes"),
    ("taco", "Main Dishes"), ("enchilada", "Main Dishes"),
    ("fajita", "Main Dishes"), ("italian chicken", "Main Dishes"),
    ("risotto", "Main Dishes"), ("high protein", "Main Dishes"),
    ("low carb", "Main Dishes"), ("keto", "Main Dishes"),
    ("mediterranean", "Main Dishes"), ("whole30", "Main Dishes"),
    ("paleo", "Main Dishes"), ("vegetarian", "Vegetarian"),
    ("vegan dinner", "Vegan"), ("vegan lunch", "Vegan"),
    ("tofu", "Vegan"), ("lentil", "Vegetarian"),
    ("chickpea", "Vegetarian"), ("healthy snack", "Snacks"),
    ("protein snack", "Snacks"), ("energy bites", "Snacks"),
    ("hummus", "Snacks"), ("trail mix", "Snacks"),
    ("side dish vegetable", "Side Dishes"), ("roasted vegetables", "Side Dishes"),
    ("steamed vegetables", "Side Dishes"), ("rice pilaf", "Side Dishes"),
    ("salad", "Salads"), ("quinoa salad", "Salads"),
    ("greek salad", "Salads"), ("slow cooker", "Main Dishes"),
    ("crockpot", "Main Dishes"), ("instant pot", "Main Dishes"),
    ("meal prep", "Main Dishes"), ("sheet pan", "Main Dishes"),
    ("one pot", "Main Dishes"), ("skillet", "Main Dishes"),
    ("grilled", "Main Dishes"), ("baked chicken", "Main Dishes"),
    ("meatloaf", "Main Dishes"), ("chili", "Soups"),
    ("pot roast", "Main Dishes"), ("stuffed peppers", "Main Dishes"),
    ("kabob skewer", "Main Dishes"), ("seafood pasta", "Seafood"),
    ("shrimp scampi", "Seafood"), ("fish tacos", "Seafood"),
    ("ceviche", "Seafood"), ("clam chowder", "Soups"),
    ("minestrone", "Soups"), ("tomato soup", "Soups"),
    ("chicken noodle soup", "Soups"), ("broccoli soup", "Soups"),
    ("sweet potato", "Side Dishes"), ("cauliflower", "Side Dishes"),
    ("zucchini", "Side Dishes"), ("asparagus", "Side Dishes"),
    ("brussels sprouts", "Side Dishes"), ("coleslaw", "Salads"),
    ("caesar salad", "Salads"), ("cobb salad", "Salads"),
    ("tuna salad", "Salads"), ("egg salad", "Main Dishes"),
    ("protein bowl", "Main Dishes"), ("buddha bowl", "Vegetarian"),
    ("poke bowl", "Seafood"), ("acai bowl", "Breakfast"),
    ("overnight oats", "Breakfast"), ("egg muffin", "Breakfast"),
    ("frittata", "Breakfast"), ("quiche", "Breakfast"),
    ("crepe", "Breakfast"), ("waffle", "Breakfast"),
]

# ===== CREATIVE QUERIES (Day 6+) =====
CUISINE_QUERIES = [
    "thai", "indian", "japanese", "korean", "chinese", "vietnamese",
    "mexican", "italian", "greek", "french", "moroccan", "ethiopian",
    "spanish", "brazilian", "peruvian", "turkish", "lebanese", "cuban",
    "cajun", "southern", "hawaiian", "caribbean", "german", "swedish",
    "polish", "russian", "filipino", "indonesian", "malaysian", "african",
]

INGREDIENT_QUERIES = [
    "avocado", "quinoa", "sweet potato", "kale", "spinach", "mushroom",
    "eggplant", "artichoke", "lamb", "duck", "venison", "bison",
    "tempeh", "seitan", "edamame", "black bean", "kidney bean",
    "butternut squash", "acorn squash", "beet", "fennel", "leek",
    "miso", "tahini", "harissa", "chimichurri", "pesto", "tzatziki",
    "coconut milk", "almond butter", "cashew", "walnut", "pecan",
    "chia seed", "flaxseed", "hemp seed", "nutritional yeast",
    "cauliflower rice", "zucchini noodles", "spaghetti squash",
    "bone broth", "kombucha", "matcha", "turmeric", "ginger root",
    "pomegranate", "dragon fruit", "jackfruit", "plantain",
]

NUTRIENT_SEARCH_RANGES = [
    # (minCal, maxCal, minProtein, maxFat, category)
    (200, 400, 20, 15, "Main Dishes"),    # Low cal, high protein, low fat
    (300, 500, 30, 20, "Main Dishes"),    # Balanced moderate
    (400, 600, 35, 25, "Main Dishes"),    # Higher cal balanced
    (100, 250, 10, 10, "Snacks"),         # Light snacks
    (150, 350, 15, 12, "Breakfast"),      # Moderate breakfast
    (300, 500, 25, 20, "Soups"),          # Hearty soups
    (200, 400, 20, 30, "Main Dishes"),    # Keto-friendly (higher fat OK)
    (250, 450, 30, 15, "Main Dishes"),    # Very high protein
    (150, 300, 8, 8, "Salads"),           # Light salads
    (200, 350, 15, 10, "Vegetarian"),     # Vegetarian moderate
]


def load_state():
    if os.path.exists(STATE_FILE):
        with open(STATE_FILE, "r") as f:
            return json.load(f)
    return {"run_count": 0, "keyword_index": 0, "last_run": None}


def save_state(state):
    with open(STATE_FILE, "w") as f:
        json.dump(state, f, indent=2)


def create_staging_tables(cursor):
    """Create staging tables if they don't exist."""
    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_Recipes')
    CREATE TABLE Staging_Recipes (
        StagingID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularID INT NOT NULL UNIQUE,
        RecipeName NVARCHAR(500) NOT NULL,
        CategoryName NVARCHAR(100) DEFAULT 'Uncategorized',
        PrepTime NVARCHAR(50), CookTime NVARCHAR(50),
        Servings NVARCHAR(50), Difficulty NVARCHAR(20),
        Source NVARCHAR(500), SourceUrl NVARCHAR(1000),
        Notes NVARCHAR(MAX), ImageUrl NVARCHAR(1000),
        DietLabels NVARCHAR(500), DishTypes NVARCHAR(500),
        Cuisines NVARCHAR(500),
        IsApproved BIT DEFAULT 0,
        ImportedAt DATETIME DEFAULT GETDATE()
    )""")
    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_RecipeIngredients')
    CREATE TABLE Staging_RecipeIngredients (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularRecipeID INT NOT NULL, SortOrder INT DEFAULT 0,
        IngredientGroup NVARCHAR(100), Description NVARCHAR(500) NOT NULL,
        OriginalName NVARCHAR(200), Amount DECIMAL(10,2),
        Unit NVARCHAR(50), AisleName NVARCHAR(100)
    )""")
    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_RecipeDirections')
    CREATE TABLE Staging_RecipeDirections (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularRecipeID INT NOT NULL, StepNumber INT NOT NULL,
        DirectionGroup NVARCHAR(200), Instruction NVARCHAR(MAX) NOT NULL
    )""")
    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_RecipeNutrition')
    CREATE TABLE Staging_RecipeNutrition (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularRecipeID INT NOT NULL UNIQUE,
        CaloriesPerServing INT, TotalFatGrams DECIMAL(10,2),
        SaturatedFatGrams DECIMAL(10,2), CholesterolMg DECIMAL(10,2),
        SodiumMg DECIMAL(10,2), TotalCarbsGrams DECIMAL(10,2),
        FiberGrams DECIMAL(10,2), SugarGrams DECIMAL(10,2),
        ProteinGrams DECIMAL(10,2), ServingSizeNote NVARCHAR(200)
    )""")
    cursor.execute("""
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staging_RecipeTags')
    CREATE TABLE Staging_RecipeTags (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SpoonacularRecipeID INT NOT NULL, TagName NVARCHAR(100) NOT NULL
    )""")
    # Fix DirectionGroup column size if too small from previous version
    cursor.execute("""
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Staging_RecipeDirections')
               AND name = 'DirectionGroup' AND max_length < 400)
        ALTER TABLE Staging_RecipeDirections ALTER COLUMN DirectionGroup NVARCHAR(200)
    """)
    cursor.connection.commit()


def search_recipes(query, number=100, offset=0):
    url = f"{BASE_URL}/recipes/complexSearch"
    params = {
        "apiKey": API_KEY, "query": query, "number": number, "offset": offset,
        "addRecipeNutrition": True, "addRecipeInformation": True,
        "fillIngredients": True, "instructionsRequired": True,
    }
    resp = requests.get(url, params=params, timeout=30)
    resp.raise_for_status()
    return resp.json().get("results", [])


def search_by_nutrients(min_cal, max_cal, min_protein=0, max_fat=999, number=100, offset=0):
    url = f"{BASE_URL}/recipes/findByNutrients"
    params = {
        "apiKey": API_KEY, "number": number, "offset": offset,
        "minCalories": min_cal, "maxCalories": max_cal,
        "minProtein": min_protein, "maxFat": max_fat,
    }
    resp = requests.get(url, params=params, timeout=30)
    resp.raise_for_status()
    return resp.json()


def get_random_recipes(number=20, tags=""):
    url = f"{BASE_URL}/recipes/random"
    params = {"apiKey": API_KEY, "number": number}
    if tags:
        params["tags"] = tags
    resp = requests.get(url, params=params, timeout=30)
    resp.raise_for_status()
    return resp.json().get("recipes", [])


def get_recipe_info(recipe_id):
    url = f"{BASE_URL}/recipes/{recipe_id}/information"
    params = {"apiKey": API_KEY, "includeNutrition": True}
    resp = requests.get(url, params=params, timeout=30)
    resp.raise_for_status()
    return resp.json()


def extract_nutrient(nutrients, name):
    for n in nutrients:
        if n.get("name", "").lower() == name.lower():
            return n.get("amount")
    return None


def insert_recipe(cursor, recipe, category):
    import re
    spoon_id = recipe.get("id")
    if not spoon_id:
        return False

    cursor.execute("SELECT COUNT(*) FROM Staging_Recipes WHERE SpoonacularID = ?", spoon_id)
    if cursor.fetchone()[0] > 0:
        return False

    title = recipe.get("title", "Unknown")
    prep_min = recipe.get("preparationMinutes") or -1
    cook_min = recipe.get("cookingMinutes") or -1
    ready_min = recipe.get("readyInMinutes") or 0
    servings = recipe.get("servings") or 1
    source = recipe.get("sourceName", "")
    source_url = recipe.get("sourceUrl", "")
    summary = recipe.get("summary", "")
    image = recipe.get("image", "")
    diets = recipe.get("diets", [])
    dish_types = recipe.get("dishTypes", [])
    cuisines = recipe.get("cuisines", [])

    summary = re.sub(r'<[^>]+>', '', summary) if summary else ""
    if len(summary) > 2000:
        summary = summary[:2000] + "..."

    prep_time = f"{prep_min} min" if prep_min > 0 else (f"{ready_min} min" if ready_min > 0 else None)
    cook_time = f"{cook_min} min" if cook_min > 0 else None

    cursor.execute("""
        INSERT INTO Staging_Recipes
        (SpoonacularID, RecipeName, CategoryName, PrepTime, CookTime, Servings,
         Source, SourceUrl, Notes, ImageUrl, DietLabels, DishTypes, Cuisines)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """, spoon_id, title, category, prep_time, cook_time, str(servings),
         source, source_url, summary, image,
         ", ".join(diets), ", ".join(dish_types), ", ".join(cuisines))

    for i, ing in enumerate(recipe.get("extendedIngredients", [])):
        cursor.execute("""
            INSERT INTO Staging_RecipeIngredients
            (SpoonacularRecipeID, SortOrder, Description, OriginalName, Amount, Unit, AisleName)
            VALUES (?, ?, ?, ?, ?, ?, ?)
        """, spoon_id, i + 1, ing.get("original", ing.get("name", "Unknown")),
             ing.get("name", ""), ing.get("amount", 0), ing.get("unit", ""), ing.get("aisle", ""))

    nutrients = recipe.get("nutrition", {}).get("nutrients", [])
    if nutrients:
        cursor.execute("""
            INSERT INTO Staging_RecipeNutrition
            (SpoonacularRecipeID, CaloriesPerServing, TotalFatGrams, SaturatedFatGrams,
             CholesterolMg, SodiumMg, TotalCarbsGrams, FiberGrams, SugarGrams,
             ProteinGrams, ServingSizeNote)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """, spoon_id,
             int(extract_nutrient(nutrients, "Calories")) if extract_nutrient(nutrients, "Calories") else None,
             extract_nutrient(nutrients, "Fat"), extract_nutrient(nutrients, "Saturated Fat"),
             extract_nutrient(nutrients, "Cholesterol"), extract_nutrient(nutrients, "Sodium"),
             extract_nutrient(nutrients, "Carbohydrates"), extract_nutrient(nutrients, "Fiber"),
             extract_nutrient(nutrients, "Sugar"), extract_nutrient(nutrients, "Protein"),
             f"{servings} serving(s)")

    step_num = 0
    for ig in recipe.get("analyzedInstructions", []):
        group_name = (ig.get("name", "") or "")[:200]
        for step in ig.get("steps", []):
            step_num += 1
            cursor.execute("""
                INSERT INTO Staging_RecipeDirections
                (SpoonacularRecipeID, StepNumber, DirectionGroup, Instruction)
                VALUES (?, ?, ?, ?)
            """, spoon_id, step_num, group_name if group_name else None, step.get("step", ""))

    all_tags = set(diets + dish_types + cuisines)
    for tag in all_tags:
        if tag:
            cursor.execute("INSERT INTO Staging_RecipeTags (SpoonacularRecipeID, TagName) VALUES (?, ?)",
                           spoon_id, tag.strip()[:100])

    return True


def run_keyword_search(cursor, conn, state):
    """Days 1-5: systematic keyword search."""
    total_imported = 0
    start_idx = state.get("keyword_index", 0)

    for i in range(start_idx, len(KEYWORD_QUERIES)):
        query, category = KEYWORD_QUERIES[i]

        for page in range(PAGES_PER_QUERY):
            offset = page * RECIPES_PER_QUERY
            log.info(f"Keyword: '{query}' -> {category} (page {page + 1})")

            try:
                recipes = search_recipes(query, RECIPES_PER_QUERY, offset)
            except requests.exceptions.HTTPError as e:
                status = getattr(e.response, 'status_code', None) or (402 if '402' in str(e) else None)
                if status == 402:
                    log.warning("Quota exceeded. Saving progress.")
                    state["keyword_index"] = i
                    return total_imported, True
                log.error(f"HTTP error: {e}")
                break
            except Exception as e:
                log.error(f"Error: {e}")
                break

            if not recipes:
                break

            page_imported = 0
            for recipe in recipes:
                try:
                    if insert_recipe(cursor, recipe, category):
                        page_imported += 1
                        total_imported += 1
                except Exception:
                    pass

            conn.commit()
            log.info(f"  Imported {page_imported} new recipes")

            if len(recipes) < RECIPES_PER_QUERY:
                break
            time.sleep(1.5)

        state["keyword_index"] = i + 1

    return total_imported, False


def run_creative_search(cursor, conn):
    """Day 6+: random recipes, cuisine-based, nutrient-based searches."""
    total_imported = 0

    # 1. Random recipes (several batches with different tags)
    random_tags = ["main course", "breakfast", "dinner", "lunch", "snack",
                   "side dish", "salad", "soup", "appetizer", "dessert"]
    random.shuffle(random_tags)

    for tag in random_tags[:5]:
        log.info(f"Random recipes with tag: '{tag}'")
        try:
            recipes = get_random_recipes(20, tag)
            for recipe in recipes:
                try:
                    if insert_recipe(cursor, recipe, "Main Dishes"):
                        total_imported += 1
                except Exception:
                    pass
            conn.commit()
            log.info(f"  Imported from random batch")
            time.sleep(1.5)
        except requests.exceptions.HTTPError as e:
            if e.response and e.response.status_code == 402:
                log.warning("Quota exceeded.")
                return total_imported
            log.error(f"Error: {e}")
        except Exception as e:
            log.error(f"Error: {e}")

    # 2. Cuisine-based searches
    random.shuffle(CUISINE_QUERIES)
    for cuisine in CUISINE_QUERIES[:10]:
        log.info(f"Cuisine search: '{cuisine}'")
        try:
            recipes = search_recipes(cuisine, 50)
            for recipe in recipes:
                try:
                    cat = "Main Dishes"
                    dish_types = recipe.get("dishTypes", [])
                    if "breakfast" in dish_types:
                        cat = "Breakfast"
                    elif "snack" in dish_types or "appetizer" in dish_types:
                        cat = "Snacks"
                    elif "salad" in dish_types:
                        cat = "Salads"
                    elif "soup" in dish_types:
                        cat = "Soups"
                    if insert_recipe(cursor, recipe, cat):
                        total_imported += 1
                except Exception:
                    pass
            conn.commit()
            log.info(f"  Imported from cuisine search")
            time.sleep(1.5)
        except requests.exceptions.HTTPError as e:
            if e.response and e.response.status_code == 402:
                log.warning("Quota exceeded.")
                return total_imported
        except Exception as e:
            log.error(f"Error: {e}")

    # 3. Ingredient-based searches
    random.shuffle(INGREDIENT_QUERIES)
    for ingredient in INGREDIENT_QUERIES[:10]:
        log.info(f"Ingredient search: '{ingredient}'")
        try:
            recipes = search_recipes(ingredient, 50)
            for recipe in recipes:
                try:
                    if insert_recipe(cursor, recipe, "Main Dishes"):
                        total_imported += 1
                except Exception:
                    pass
            conn.commit()
            time.sleep(1.5)
        except requests.exceptions.HTTPError as e:
            if e.response and e.response.status_code == 402:
                log.warning("Quota exceeded.")
                return total_imported
        except Exception as e:
            log.error(f"Error: {e}")

    return total_imported


def main():
    log.info("=" * 60)
    log.info(f"Spoonacular Daily Import - {datetime.now().strftime('%Y-%m-%d %H:%M')}")
    log.info("=" * 60)

    conn = pyodbc.connect(SQL_CONNECTION)
    cursor = conn.cursor()
    create_staging_tables(cursor)

    state = load_state()
    state["run_count"] = state.get("run_count", 0) + 1
    run_num = state["run_count"]

    cursor.execute("SELECT COUNT(*) FROM Staging_Recipes")
    before_count = cursor.fetchone()[0]
    log.info(f"Run #{run_num} | Recipes before: {before_count}")

    if state.get("keyword_index", 0) < len(KEYWORD_QUERIES):
        # Still have keyword queries to finish
        log.info("Mode: Keyword search (building core library)")
        imported, quota_hit = run_keyword_search(cursor, conn, state)
    else:
        # Keywords done -- creative mode
        log.info("Mode: Creative search (discovering new recipes)")
        imported = run_creative_search(cursor, conn)

    cursor.execute("SELECT COUNT(*) FROM Staging_Recipes")
    after_count = cursor.fetchone()[0]

    cursor.execute("""
        SELECT COUNT(*) FROM Staging_Recipes r
        WHERE EXISTS (SELECT 1 FROM Staging_RecipeNutrition n WHERE n.SpoonacularRecipeID = r.SpoonacularID)
    """)
    with_nutrition = cursor.fetchone()[0]

    state["last_run"] = datetime.now().isoformat()
    save_state(state)

    log.info(f"")
    log.info(f"Results:")
    log.info(f"  New recipes: {imported}")
    log.info(f"  Total in staging: {after_count}")
    log.info(f"  With nutrition: {with_nutrition}")
    log.info(f"  Keyword progress: {state.get('keyword_index', 0)}/{len(KEYWORD_QUERIES)}")
    log.info(f"=" * 60)

    cursor.close()
    conn.close()


if __name__ == "__main__":
    main()

"""
Wikibooks Cookbook Importer
===========================
Loads the CC-BY-SA 4.0 Wikibooks Cookbook dataset
(https://huggingface.co/datasets/gossminn/wikibooks-cookbook)
into the WIKIBOOKS_* staging tables in RecipeDB.

Usage:
    Set env var first:
      RECIPEDB_SQL_CONNECTION="DRIVER={ODBC Driver 17 for SQL Server};SERVER=localhost;DATABASE=RecipeDB;UID=...;PWD=...;TrustServerCertificate=yes;"

    python wikibooks_import.py --init     # Create WIKIBOOKS_ tables (runs schema.sql)
    python wikibooks_import.py            # Import; preserves existing rows
    python wikibooks_import.py --reload   # Wipe WIKIBOOKS_* tables then import

Nutrition is NOT computed by this script. Run wikibooks_compute_nutrition.py
after a successful import.
"""

import json
import os
import sys
import urllib.parse
from pathlib import Path

import pyodbc

HERE = Path(__file__).parent
JSON_PATH = HERE / "wikibooks_cookbook" / "recipes_parsed.json"
SCHEMA_PATH = HERE / "wikibooks_cookbook" / "schema.sql"

INGREDIENT_SECTIONS = {
    "ingredients", "ingredient", "ingredients:", "ingredient list",
}
DIRECTION_SECTIONS = {
    "procedure", "procedures", "preparation", "instructions",
    "method", "directions",
}
NOTES_SECTIONS = {
    "notes, tips, and variations", "notes, tips and variations",
    "notes", "variations", "tips",
}


def get_conn():
    cs = os.environ.get("RECIPEDB_SQL_CONNECTION", "")
    if not cs:
        sys.exit("RECIPEDB_SQL_CONNECTION env var is not set.")
    return pyodbc.connect(cs, autocommit=False)


def run_schema(conn):
    sql = SCHEMA_PATH.read_text(encoding="utf-8")
    cur = conn.cursor()
    cur.execute(sql)
    conn.commit()


def normalize_category_name(url: str) -> str:
    if not url:
        return "Uncategorized"
    name = url
    if "Category:" in name:
        name = name.split("Category:", 1)[1]
    name = urllib.parse.unquote(name)
    # Strip any URL query/fragment params (e.g. '&action=edit&redlink=1' from
    # red-linked broken Wikibooks category pages)
    for sep in ("&", "?", "#"):
        if sep in name:
            name = name.split(sep, 1)[0]
    name = name.replace("_", " ").strip()
    return name or "Uncategorized"


def clean_str(v, max_len=None):
    if v is None:
        return None
    s = str(v).strip()
    if not s:
        return None
    if max_len is not None:
        s = s[:max_len]
    return s


def section_key(section):
    return (section or "").strip().lower()


def parse_recipe(rd):
    """Pull ingredient lines, numbered directions, and notes text from text_lines."""
    ingredients = []
    directions = []
    notes_lines = []
    step = 0
    for line in rd.get("text_lines", []):
        text = (line.get("text") or "").strip()
        if not text:
            continue
        lt = (line.get("line_type") or "").lower()
        sk = section_key(line.get("section"))
        if sk in INGREDIENT_SECTIONS and lt == "ul":
            ingredients.append(text)
        elif sk in DIRECTION_SECTIONS and lt == "ol":
            step += 1
            directions.append((step, text))
        elif sk in NOTES_SECTIONS:
            notes_lines.append(text)
    notes = "\n".join(notes_lines) if notes_lines else None
    return ingredients, directions, notes


def get_or_create_category(cur, name, url):
    cur.execute(
        "SELECT WikibooksCategoryID FROM WIKIBOOKS_Categories WHERE CategoryName = ?",
        (name,),
    )
    row = cur.fetchone()
    if row:
        return row[0]
    cur.execute(
        "INSERT INTO WIKIBOOKS_Categories (CategoryName, CategoryUrl) "
        "OUTPUT INSERTED.WikibooksCategoryID VALUES (?, ?)",
        (name, url),
    )
    return cur.fetchone()[0]


def wipe(conn):
    cur = conn.cursor()
    print("Wiping WIKIBOOKS_* tables...")
    cur.execute("DELETE FROM WIKIBOOKS_RecipeNutrition")
    cur.execute("DELETE FROM WIKIBOOKS_RecipeDirections")
    cur.execute("DELETE FROM WIKIBOOKS_RecipeIngredients")
    cur.execute("DELETE FROM WIKIBOOKS_Recipes")
    cur.execute("DELETE FROM WIKIBOOKS_Categories")
    conn.commit()


def main():
    args = set(sys.argv[1:])
    do_init = "--init" in args
    do_reload = "--reload" in args

    print(f"Loading {JSON_PATH}")
    data = json.loads(JSON_PATH.read_text(encoding="utf-8"))
    print(f"  {len(data)} recipes in source dataset")

    conn = get_conn()
    cur = conn.cursor()

    if do_init:
        print("Running schema.sql")
        run_schema(conn)

    if do_reload:
        wipe(conn)

    print("Building category lookup")
    cat_map = {}
    for r in data:
        ib = r["recipe_data"].get("infobox") or {}
        url = ib.get("category") or ""
        if url and url not in cat_map:
            name = normalize_category_name(url)
            cat_map[url] = get_or_create_category(cur, name, url)
    uncat_id = get_or_create_category(cur, "Uncategorized", None)
    conn.commit()
    print(f"  {len(cat_map)} unique categories + Uncategorized fallback")

    print("Importing recipes")
    imported = 0
    skipped = 0
    skip_reasons = {}

    for r in data:
        rd = r["recipe_data"]
        title = clean_str(rd.get("title"), 500)
        if not title:
            skipped += 1
            skip_reasons["no_title"] = skip_reasons.get("no_title", 0) + 1
            continue

        ingredients, directions, notes = parse_recipe(rd)
        if not ingredients and not directions:
            skipped += 1
            skip_reasons["no_content"] = skip_reasons.get("no_content", 0) + 1
            continue

        ib = rd.get("infobox") or {}
        cat_id = cat_map.get(ib.get("category") or "", uncat_id)

        cur.execute(
            """
            INSERT INTO WIKIBOOKS_Recipes
                (WikibooksCategoryID, RecipeName, CookTime, Servings, Difficulty,
                 Source, Notes, SourceFilename)
            OUTPUT INSERTED.WikibooksRecipeID
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                cat_id,
                title,
                clean_str(ib.get("time"), 100),
                clean_str(ib.get("servings"), 50),
                clean_str(ib.get("difficulty"), 50),
                clean_str(rd.get("url"), 500) or "",
                notes,
                clean_str(r.get("filename"), 200),
            ),
        )
        rid = cur.fetchone()[0]

        for i, desc in enumerate(ingredients, start=1):
            cur.execute(
                "INSERT INTO WIKIBOOKS_RecipeIngredients "
                "(WikibooksRecipeID, SortOrder, Description) VALUES (?, ?, ?)",
                (rid, i, desc[:500]),
            )

        for step_no, instr in directions:
            cur.execute(
                "INSERT INTO WIKIBOOKS_RecipeDirections "
                "(WikibooksRecipeID, StepNumber, Instruction) VALUES (?, ?, ?)",
                (rid, step_no, instr),
            )

        imported += 1
        if imported % 250 == 0:
            conn.commit()
            print(f"  ...{imported} imported")

    conn.commit()
    print(f"\nDONE")
    print(f"  imported : {imported}")
    print(f"  skipped  : {skipped}")
    for reason, count in skip_reasons.items():
        print(f"    - {reason}: {count}")
    print("\nNext step: review imported rows, then run wikibooks_compute_nutrition.py")


if __name__ == "__main__":
    main()

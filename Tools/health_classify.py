"""Health-consciousness classifier oracle for the Wikibooks recipe catalog.

Goal: identify recipes that are DEFINITELY health-conscious for a fitness app,
and flag indulgent junk (cake, candy, deep-fried, sugar bombs).

NOTE: the SHIPPING classifier is Data/RecipeHealth.cs, which runs on-device from
the seeded nutrition (no DB/server). This script is the analysis oracle used to
review/tune the thresholds and to emit health_classification.json for the review
dashboard. The two are kept byte-for-byte in sync (parity-tested). If you change a
threshold here, change it in RecipeHealth.cs too.

Approach = category signal (soft nudge) + per-serving nutrition thresholds (sugar
is the primary lever). Outputs a HealthScore (0-100), a 3-tier label, a healthy-
treat flag, and a hard-remove flag. Read-only against RecipeDB; writes only JSON.
"""
import pyodbc, collections, re

CS = ('DRIVER={ODBC Driver 17 for SQL Server};SERVER=localhost\\DEVTEST;'
      'DATABASE=RecipeDB;Trusted_Connection=yes;')

# ---- category signals ----------------------------------------------------
# Categories that are inherently indulgent (a fitness cookbook wouldn't feature them).
JUNK_CAT = re.compile(r"\b(dessert|cake|cookie|candy|candies|confection|fudge|toffee|"
                      r"truffle|caramel|brownie|doughnut|donut|frosting|icing|"
                      r"cheesecake|pastry|pie and tart|\btart|pudding|custard|"
                      r"ice cream|gelato|sorbet|mousse|marshmallow|sweet|praline|"
                      r"macaron|meringue|cobbler|souffl)", re.I)
FRIED_CAT = re.compile(r"\b(fritter|deep.?fried|churro)", re.I)
FRIED_NAME = re.compile(r"\b(deep.?fried|deep fry|churro|funnel cake|corn dog|corndog)", re.I)
# Name-level junk that can appear inside a non-dessert category
JUNK_NAME = re.compile(r"\b(cake|cookie|candy|fudge|brownie|doughnut|donut|frosting|"
                       r"icing|cheesecake|truffle|toffee|caramel|ice cream|gelato|"
                       r"sorbet|mousse|marshmallow|macaron|praline|syrup|"
                       r"buttercream|meringue|custard|pudding|éclair|eclair)", re.I)


def classify(r):
    """Return (score 0-100, tier, is_healthy, reasons[])."""
    cat = r["cat"] or ""
    name = r["name"] or ""
    reasons = []
    score = 60  # neutral baseline

    junk_cat = bool(JUNK_CAT.search(cat)) or bool(JUNK_NAME.search(name))
    fried = bool(FRIED_CAT.search(cat)) or bool(FRIED_NAME.search(name))

    cal = r.get("cal"); sugar = f(r.get("sugar")); satfat = f(r.get("satfat"))
    sodium = f(r.get("sodium")); fiber = f(r.get("fiber")); protein = f(r.get("protein"))

    # ---- category / prep nudges (modest — sugar does the real filtering so good
    #      macros can redeem a dessert) ----
    if junk_cat:
        score -= 12; reasons.append("sweet/dessert category")
    if fried:
        score -= 15; reasons.append("deep-fried")

    # ---- nutrition penalties (per serving) — SUGAR is the primary lever ----
    if sugar is not None:
        if sugar >= 40: score -= 38; reasons.append(f"very high sugar {sugar:.0f}g")
        elif sugar >= 25: score -= 24; reasons.append(f"high sugar {sugar:.0f}g")
        elif sugar >= 15: score -= 11; reasons.append(f"moderate sugar {sugar:.0f}g")
        elif sugar >= 8: score -= 3
    if satfat is not None:
        if satfat >= 18: score -= 20; reasons.append(f"very high sat-fat {satfat:.0f}g")
        elif satfat >= 11: score -= 10; reasons.append(f"high sat-fat {satfat:.0f}g")
        elif satfat >= 6: score -= 3
    if sodium is not None:
        if sodium >= 1800: score -= 15; reasons.append(f"very high sodium {sodium:.0f}mg")
        elif sodium >= 1100: score -= 7; reasons.append(f"high sodium {sodium:.0f}mg")
    if cal is not None:
        if cal >= 800: score -= 10; reasons.append(f"calorie-dense {cal}/serving")
        elif cal >= 650: score -= 5

    # ---- nutrition bonuses (redeeming signals) ----
    if fiber is not None and fiber >= 5: score += 10; reasons.append(f"high fiber {fiber:.0f}g")
    elif fiber is not None and fiber >= 3: score += 5
    if protein is not None and protein >= 20: score += 15; reasons.append(f"high protein {protein:.0f}g")
    elif protein is not None and protein >= 10: score += 7

    score = max(0, min(100, score))

    # ---- tier ----
    if score >= 65:
        tier = "Healthy"
    elif score >= 45:
        tier = "Moderate"
    else:
        tier = "Indulgent"

    # "Definitely health-conscious" is NUTRITION-primary: category is only a soft
    # score signal, so a low-sugar / high-protein dessert (e.g. a protein truffle)
    # can still qualify. The gate is: not a sugar/sat-fat/sodium bomb, decent score.
    is_healthy = (
        not fried
        and (sugar is None or sugar < 15)
        and (satfat is None or satfat < 13)
        and (sodium is None or sodium < 1400)
        and score >= 62
    )
    # A "healthy treat" = a sweet/dessert-category recipe that nonetheless clears the
    # nutrition bar (the protein-truffle case). Surfaced separately so the fitness
    # cookbook can offer smart treats without the sugar bombs.
    healthy_treat = is_healthy and junk_cat
    # "Hard-remove" = the clearest junk to physically drop from the bundle: a sweet/
    # dessert category, NOT redeemed by nutrition, and either a real sugar bomb or a
    # pure-confection type. Conservative so nothing borderline gets deleted.
    hard_remove = (
        junk_cat and not is_healthy and not healthy_treat
        and (protein is None or protein < 12)
        and (sugar is None or sugar >= 12)
    )
    return score, tier, is_healthy, healthy_treat, hard_remove, reasons


def f(x):
    return None if x is None else float(x)


def main():
    cn = pyodbc.connect(CS); c = cn.cursor()
    c.execute("""SELECT r.WikibooksRecipeID, r.RecipeName, cat.CategoryName
                 FROM WIKIBOOKS_Recipes r JOIN WIKIBOOKS_Categories cat
                 ON r.WikibooksCategoryID=cat.WikibooksCategoryID WHERE r.IsExcluded=0""")
    recs = {rid: {"name": nm, "cat": cn2 or ""} for rid, nm, cn2 in c.fetchall()}
    c.execute("""SELECT WikibooksRecipeID, CaloriesPerServing, ProteinGrams, TotalCarbsGrams,
                 TotalFatGrams, FiberGrams, SodiumMg, SaturatedFatGrams, SugarGrams
                 FROM WIKIBOOKS_RecipeNutrition""")
    for row in c.fetchall():
        if row[0] in recs:
            recs[row[0]].update(cal=row[1], protein=row[2], carbs=row[3], fat=row[4],
                                fiber=row[5], sodium=row[6], satfat=row[7], sugar=row[8])

    tiers = collections.Counter(); healthy = 0; treats = []; hard = 0
    samples = {"Healthy": [], "Moderate": [], "Indulgent": []}
    out = []
    for rid, r in recs.items():
        score, tier, is_h, is_treat, is_hard, reasons = classify(r)
        tiers[tier] += 1
        r["_tier"] = tier; r["_score"] = score; r["_healthy"] = is_h; r["_reasons"] = reasons
        if is_h: healthy += 1
        if is_hard: hard += 1
        if is_treat: treats.append((score, r["name"], r["cat"],
                                    f(r.get("sugar")), f(r.get("protein"))))
        if len(samples[tier]) < 40:
            samples[tier].append((r["_score"], r["name"], r["cat"]))
        out.append({
            "id": rid, "name": r["name"], "cat": r["cat"], "score": score,
            "tier": tier, "healthy": is_h, "treat": is_treat, "hardRemove": is_hard,
            "cal": r.get("cal"), "sugar": f(r.get("sugar")), "satfat": f(r.get("satfat")),
            "sodium": f(r.get("sodium")), "fiber": f(r.get("fiber")),
            "protein": f(r.get("protein")), "reasons": reasons,
        })

    import json, pathlib
    jp = pathlib.Path(__file__).parent / "health_classification.json"
    jp.write_text(json.dumps({"count": len(out), "recipes": out}, ensure_ascii=False),
                  encoding="utf-8")
    print(f"[wrote {jp} — {len(out)} recipes, {hard} hard-remove]")

    print(f"=== {len(recs)} active recipes ===")
    print("\nTier distribution:")
    for t in ("Healthy", "Moderate", "Indulgent"):
        print(f"  {t:10}: {tiers[t]:5}  ({100*tiers[t]/len(recs):.0f}%)")
    print(f"\n'Definitely health-conscious' (IsHealthy=true): {healthy}  "
          f"({100*healthy/len(recs):.0f}%)")
    print(f"'Healthy treats' (sweet category that clears the nutrition bar): {len(treats)}")

    print("\n--- HEALTHY TREATS (dessert/sweet but nutrition-approved) ---")
    for s, n, cat, sug, pro in sorted(treats, reverse=True)[:25]:
        print(f"  {s:3}  {n[:42]:42}  sugar={sug or 0:.0f}g pro={pro or 0:.0f}g  [{cat}]")

    print("\n--- sample HEALTHY (top) ---")
    for s, n, cat in sorted(samples["Healthy"], reverse=True)[:15]:
        print(f"  {s:3}  {n[:45]:45}  [{cat}]")
    print("\n--- sample INDULGENT (lowest) ---")
    for s, n, cat in sorted(samples["Indulgent"])[:18]:
        print(f"  {s:3}  {n[:45]:45}  [{cat}]")

    # per-category healthy rate (biggest categories) — sanity that meat/veg/soup
    # score healthy and dessert/cake/cookie score indulgent
    print("\n--- Healthy rate by category (categories with >=20 recipes) ---")
    cat_tot = collections.Counter(r["cat"] for r in recs.values())
    cat_ok = collections.Counter(r["cat"] for r in recs.values() if r["_healthy"])
    for cat, tot in cat_tot.most_common():
        if tot < 20: continue
        print(f"  {100*cat_ok[cat]/tot:3.0f}% healthy  ({cat_ok[cat]:3}/{tot:3})  {cat}")


if __name__ == "__main__":
    main()

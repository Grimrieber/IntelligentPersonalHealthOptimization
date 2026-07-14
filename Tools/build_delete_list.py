"""
Turn the 102 same-dish name clusters into an EXACT delete list, honoring the
user's rule: prune genuine redundancy ("don't need 5 meatloaf"), but KEEP
variants that carry real meaning (dietary tags feed the app's diet filter;
distinct regional/cultural dishes are not duplicates).

Per cluster:
  - members with a DIETARY tag       -> KEEP (vegan / gluten-free / egg-free /
                                          dairy-free / vegetarian / lower-sugar /
                                          with|without dates)
  - REGIONAL/whole-cluster-distinct  -> KEEP ALL (whitelisted dish keys)
  - everything else (I/II/III, bare, cooking-method-only) -> keep ONE survivor,
    delete the rest. Survivor = a member with no roman numeral if one exists,
    else the lowest id.

Prints the delete list + keep-for-reason list. Writes tools/delete_list.json.
"""
import json, io, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REP  = os.path.join(ROOT, "tools", "dedup_report.json")
OUT  = os.path.join(ROOT, "tools", "delete_list.json")

DIET = re.compile(r"\b(vegan|gluten[- ]free|egg[- ]free|dairy[- ]free|vegetarian|"
                  r"lower[- ]sugar|with dates|without dates)\b", re.I)
ROMAN = re.compile(r"\b(i{1,3}|iv|v|vi{1,3}|ix|x)\b\s*$", re.I)

# whole clusters that are genuinely DISTINCT dishes (regional/cultural/shape) -> keep all
KEEP_ALL = {
    "charoset", "coconut rice", "salt cod and potato casserole", "crullers",
    "lemon meringue pie",            # American vs British vs Vegan
    "sambar",                        # generic vs Kerala/Tamil style
    "mushroom soup",                 # generic vs Gabi Supa (diff dish)
    "sticky toffee pudding",         # with vs without dates
}


def survivor(members):
    # prefer a member with no trailing roman numeral; else lowest id
    plain = [m for m in members if not ROMAN.search(m[1])]
    pool = plain if plain else members
    return min(pool, key=lambda m: m[0])


def main():
    rep = json.load(io.open(REP, encoding="utf-8"))
    nc = rep["name_clusters"]
    delete, keep_diet, keep_regional = [], [], []
    for c in nc:
        dish, members = c["dish"], [tuple(m) for m in c["members"]]
        if dish in KEEP_ALL:
            keep_regional += [(dish, i, n) for i, n in members]
            continue
        diet = [m for m in members if DIET.search(m[1])]
        nondiet = [m for m in members if not DIET.search(m[1])]
        keep_diet += [(dish, i, n) for i, n in diet]
        if not nondiet:
            continue
        keep = survivor(nondiet)
        for m in nondiet:
            if m == keep:
                continue
            delete.append({"dish": dish, "id": m[0], "name": m[1],
                           "kept": {"id": keep[0], "name": keep[1]}})
    delete.sort(key=lambda d: d["id"])
    json.dump({"delete": delete,
               "keep_dietary": keep_diet,
               "keep_regional": keep_regional},
              io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    def sp(s): sys.stdout.buffer.write((s + "\n").encode("utf-8", "replace"))
    sp(f"DELETE {len(delete)} redundant recipes (keep 1 per dish):")
    for d in delete:
        sp(f"  del {d['id']:>4}  {d['name']:<48}  (keep {d['kept']['id']} {d['kept']['name']})")
    sp(f"\nKEEP {len(keep_diet)} dietary variants (feed diet filter):")
    for dish, i, n in keep_diet:
        sp(f"  keep {i:>4}  {n}")
    sp(f"\nKEEP {len(keep_regional)} regional/distinct variants:")
    for dish, i, n in keep_regional:
        sp(f"  keep {i:>4}  {n}")
    print(f"\nwrote {OUT}")


if __name__ == "__main__":
    main()

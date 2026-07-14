using System.Text.RegularExpressions;

namespace IntelligentPersonalHealthOptimization.Data;

/// <summary>
/// On-device health-consciousness classifier for the recipe catalog. Scores each
/// recipe on a fitness lens from data already seeded on the phone (per-serving
/// sugar, saturated fat, sodium, calories, with fiber and protein as redeeming
/// signals). Category is only a soft nudge, so a lean high-protein sweet (a
/// "healthy treat") can still pass, while sugar bombs are penalised regardless of
/// category. Pure/deterministic — no network, no server, no bundle rebuild.
///
/// Mirrors Tools/health_classify.py (the analysis oracle used to review/tune the
/// thresholds). Keep the two in sync if the thresholds change.
/// </summary>
public static class RecipeHealth
{
    public const string Healthy = "Healthy";
    public const string Moderate = "Moderate";
    public const string Indulgent = "Indulgent";

    private static readonly RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.Compiled;

    // Sweet/dessert categories — a soft nudge, not a veto (sugar does the real filtering).
    private static readonly Regex JunkCat = new(
        @"\b(dessert|cake|cookie|candy|candies|confection|fudge|truffle|toffee|caramel|" +
        @"brownie|doughnut|donut|frosting|icing|cheesecake|pastry|pie and tart|\btart|" +
        @"pudding|custard|ice cream|gelato|sorbet|mousse|marshmallow|sweet|praline|" +
        @"macaron|meringue|cobbler|souffl)", Opt);

    // Sweet keywords that can appear in a recipe NAME under a non-dessert category.
    private static readonly Regex JunkName = new(
        @"\b(cake|cookie|candy|fudge|brownie|doughnut|donut|frosting|icing|cheesecake|" +
        @"truffle|toffee|caramel|ice cream|gelato|sorbet|mousse|marshmallow|macaron|" +
        @"praline|syrup|buttercream|meringue|custard|pudding|éclair|eclair)", Opt);

    private static readonly Regex FriedCat = new(@"\b(fritter|deep.?fried|churro)", Opt);
    private static readonly Regex FriedName = new(
        @"\b(deep.?fried|deep fry|churro|funnel cake|corn dog|corndog)", Opt);

    public readonly record struct Result(int Score, string Tier, bool IsTreat, bool HardRemove);

    /// <summary>
    /// Classify a recipe. Nutrition values are per serving; null = unknown (skipped).
    /// </summary>
    public static Result Classify(
        string? name, string? category,
        int? calories, double? sugar, double? satFat, double? sodium,
        double? fiber, double? protein)
    {
        var cat = category ?? string.Empty;
        var nm = name ?? string.Empty;

        bool junkCat = JunkCat.IsMatch(cat) || JunkName.IsMatch(nm);
        bool fried = FriedCat.IsMatch(cat) || FriedName.IsMatch(nm);

        int score = 60; // neutral baseline

        // ---- category / prep nudges (modest) ----
        if (junkCat) score -= 12;
        if (fried) score -= 15;

        // ---- nutrition penalties (per serving) — sugar is the primary lever ----
        if (sugar is double sg)
        {
            if (sg >= 40) score -= 38;
            else if (sg >= 25) score -= 24;
            else if (sg >= 15) score -= 11;
            else if (sg >= 8) score -= 3;
        }
        if (satFat is double sf)
        {
            if (sf >= 18) score -= 20;
            else if (sf >= 11) score -= 10;
            else if (sf >= 6) score -= 3;
        }
        if (sodium is double sd)
        {
            if (sd >= 1800) score -= 15;
            else if (sd >= 1100) score -= 7;
        }
        if (calories is int cal)
        {
            if (cal >= 800) score -= 10;
            else if (cal >= 650) score -= 5;
        }

        // ---- nutrition bonuses (redeeming signals) ----
        if (fiber is double fb)
        {
            if (fb >= 5) score += 10;
            else if (fb >= 3) score += 5;
        }
        if (protein is double pr)
        {
            if (pr >= 20) score += 15;
            else if (pr >= 10) score += 7;
        }

        if (score < 0) score = 0;
        if (score > 100) score = 100;

        string tier = score >= 65 ? Healthy : score >= 45 ? Moderate : Indulgent;

        // Strict "definitely healthy" gate (used for treat/purge logic, not the tier).
        bool isHealthy = !fried
            && (sugar is not double s2 || s2 < 15)
            && (satFat is not double f2 || f2 < 13)
            && (sodium is not double d2 || d2 < 1400)
            && score >= 62;

        bool isTreat = isHealthy && junkCat;

        // Hard-remove = the clearest junk to drop entirely: a sweet category, not
        // redeemed by nutrition, low protein, and actually sugary (or unknown sugar).
        bool hardRemove = junkCat && !isHealthy
            && (protein is not double p2 || p2 < 12)
            && (sugar is not double s3 || s3 >= 12);

        return new Result(score, tier, isTreat, hardRemove);
    }

    /// <summary>Convenience overload for a SavedRecipe-shaped row.</summary>
    public static Result Classify(Models.SavedRecipe r) => Classify(
        r.RecipeName, r.CategoryName, r.CaloriesPerServing, r.SugarGrams, r.SatFatGrams,
        r.SodiumMg, r.FiberGrams, r.ProteinGrams);
}

using System.Text.Json;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public partial class NutritionService
{
    private enum MealCategory { General, Vegetarian, Vegan, Keto }

    private record MealTemplate(
        string Name,
        MealType[] ApplicableMealTypes,
        MealCategory Category,
        (string FoodName, double ServingMultiplier)[] Components
    );

    private static MealCategory[] GetCompatibleCategories(DietType dietType) => dietType switch
    {
        DietType.Keto => [MealCategory.Keto],
        DietType.Vegan => [MealCategory.Vegan],
        DietType.Vegetarian => [MealCategory.Vegetarian, MealCategory.Vegan],
        _ => [MealCategory.General, MealCategory.Vegetarian, MealCategory.Vegan]
    };

    private static List<MealPlanItem> BuildMealFromTemplate(
        MealTemplate template, MealType mealType, double targetCalories,
        Dictionary<string, Food> foodLookup, NutritionProfile profile,
        double targetProteinG = 0, double targetCarbsG = 0, double targetFatG = 0)
    {
        var components = new List<(Food food, double baseServing)>();

        foreach (var (foodName, multiplier) in template.Components)
        {
            if (!foodLookup.TryGetValue(foodName, out var food)) continue;
            if (IsAllergyConflict(food, profile)) continue;
            components.Add((food, food.DefaultServingSize * multiplier));
        }

        if (components.Count == 0) return [];

        // If no macro targets, uniform scaling
        if (targetProteinG <= 0)
            return BuildUniformScaled(components, template.Name, mealType, targetCalories);

        // Classify each ingredient by dominant macro contribution
        var proteinGroup = new List<(Food food, double baseServing)>();
        var carbGroup = new List<(Food food, double baseServing)>();
        var fatGroup = new List<(Food food, double baseServing)>();

        foreach (var c in components)
        {
            var cal = c.food.CaloriesPer100g;
            if (cal <= 0) { carbGroup.Add(c); continue; }

            var pPct = c.food.ProteinPer100g * 4.0 / cal;
            var fPct = c.food.FatPer100g * 9.0 / cal;

            if (pPct > 0.30) proteinGroup.Add(c);
            else if (fPct > 0.45) fatGroup.Add(c);
            else carbGroup.Add(c);
        }

        // If we can't separate groups meaningfully, fall back to uniform
        if (proteinGroup.Count == 0 && fatGroup.Count == 0)
            return BuildUniformScaled(components, template.Name, mealType, targetCalories);

        // Helper: sum a macro across a group at base serving
        static double SumMacro(List<(Food food, double baseServing)> group,
            Func<Food, double> macroPer100g) =>
            group.Sum(c => macroPer100g(c.food) * c.baseServing / 100.0);

        // Uniform scale as starting baseline
        var totalBaseCal = SumMacro(components, f => f.CaloriesPer100g);
        var uniformScale = totalBaseCal > 0 ? targetCalories / totalBaseCal : 1.0;

        var proteinScale = uniformScale;
        var carbScale = uniformScale;
        var fatScale = uniformScale;

        // 1) Scale protein group to hit protein target
        if (proteinGroup.Count > 0)
        {
            var baseP = SumMacro(proteinGroup, f => f.ProteinPer100g);
            // Estimate protein contributed by other groups at uniform scale
            var otherP = SumMacro(carbGroup, f => f.ProteinPer100g) * uniformScale
                       + SumMacro(fatGroup, f => f.ProteinPer100g) * uniformScale;
            var needed = Math.Max(5, targetProteinG - otherP);
            proteinScale = baseP > 0 ? needed / baseP : uniformScale;
            proteinScale = Math.Clamp(proteinScale, 0.3, 5.0);
        }

        // 2) Scale carb group to hit carb target (accounting for protein group's carb contribution)
        if (carbGroup.Count > 0)
        {
            var baseC = SumMacro(carbGroup, f => f.CarbsPer100g);
            var otherC = SumMacro(proteinGroup, f => f.CarbsPer100g) * proteinScale
                       + SumMacro(fatGroup, f => f.CarbsPer100g) * uniformScale;
            var needed = Math.Max(5, targetCarbsG - otherC);
            carbScale = baseC > 0 ? needed / baseC : uniformScale;
            carbScale = Math.Clamp(carbScale, 0.3, 5.0);
        }

        // 3) Scale fat group to hit fat target (accounting for protein + carb groups' fat)
        if (fatGroup.Count > 0)
        {
            var baseF = SumMacro(fatGroup, f => f.FatPer100g);
            var otherF = SumMacro(proteinGroup, f => f.FatPer100g) * proteinScale
                       + SumMacro(carbGroup, f => f.FatPer100g) * carbScale;
            var needed = Math.Max(1, targetFatG - otherF);
            fatScale = baseF > 0 ? needed / baseF : uniformScale;
            fatScale = Math.Clamp(fatScale, 0.2, 5.0);
        }

        // Build items with their respective scale factors
        var items = new List<MealPlanItem>();

        void AddGroup(List<(Food food, double baseServing)> group, double scale)
        {
            foreach (var (food, baseServing) in group)
            {
                var serving = Math.Max(10, Math.Round(baseServing * scale));
                var factor = serving / 100.0;
                items.Add(new MealPlanItem
                {
                    FoodId = food.Id, MealType = mealType, MealName = template.Name,
                    ServingSizeG = serving,
                    Calories = Math.Round(food.CaloriesPer100g * factor, 1),
                    ProteinG = Math.Round(food.ProteinPer100g * factor, 1),
                    CarbsG = Math.Round(food.CarbsPer100g * factor, 1),
                    FatG = Math.Round(food.FatPer100g * factor, 1)
                });
            }
        }

        AddGroup(proteinGroup, proteinScale);
        AddGroup(carbGroup, carbScale);
        AddGroup(fatGroup, fatScale);

        return items;
    }

    private static List<MealPlanItem> BuildUniformScaled(
        List<(Food food, double baseServing)> components,
        string mealName, MealType mealType, double targetCalories)
    {
        var items = new List<MealPlanItem>();
        var baseCalories = components.Sum(c => c.food.CaloriesPer100g * c.baseServing / 100.0);
        var scaleFactor = baseCalories > 0 ? targetCalories / baseCalories : 1.0;
        scaleFactor = Math.Clamp(scaleFactor, 0.5, 5.0);

        foreach (var (food, baseServing) in components)
        {
            var serving = Math.Round(baseServing * scaleFactor);
            serving = Math.Max(serving, 15);
            var factor = serving / 100.0;
            items.Add(new MealPlanItem
            {
                FoodId = food.Id, MealType = mealType, MealName = mealName,
                ServingSizeG = serving,
                Calories = Math.Round(food.CaloriesPer100g * factor, 1),
                ProteinG = Math.Round(food.ProteinPer100g * factor, 1),
                CarbsG = Math.Round(food.CarbsPer100g * factor, 1),
                FatG = Math.Round(food.FatPer100g * factor, 1)
            });
        }

        return items;
    }

    private static bool IsAllergyConflict(Food food, NutritionProfile profile)
    {
        // Check allergies
        if (!string.IsNullOrEmpty(profile.Allergies))
        {
            var allergies = profile.Allergies.Split(',', StringSplitOptions.TrimEntries);
            if (allergies.Any(a =>
                !string.Equals(a, "None", StringComparison.OrdinalIgnoreCase) &&
                food.Name.Contains(a, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // Check foods to avoid
        if (!string.IsNullOrEmpty(profile.FoodsToAvoid))
        {
            var avoid = profile.FoodsToAvoid.Split(',', StringSplitOptions.TrimEntries);
            if (avoid.Any(a => food.Name.Contains(a, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static List<MealType> GetMealTypesForCount(int mealsPerDay, NutritionProfile? profile = null)
    {
        // Parse eating patterns to determine which meals to skip
        var skipBreakfast = false;
        var skipLunch = false;

        if (profile != null && !string.IsNullOrEmpty(profile.EatingPatternsJson))
        {
            try
            {
                var patterns = JsonSerializer.Deserialize<List<EatingPattern>>(profile.EatingPatternsJson);
                if (patterns != null)
                {
                    skipBreakfast = patterns.Contains(EatingPattern.SkipBreakfast);
                    skipLunch = patterns.Contains(EatingPattern.SkipLunch);
                }
            }
            catch { /* use defaults */ }
        }

        // Build the full meal list for the count
        var meals = mealsPerDay switch
        {
            1 => new List<MealType> { MealType.Dinner },
            2 => new List<MealType> { MealType.Lunch, MealType.Dinner },
            3 => new List<MealType> { MealType.Breakfast, MealType.Lunch, MealType.Dinner },
            4 => new List<MealType> { MealType.Breakfast, MealType.Lunch, MealType.AfternoonSnack, MealType.Dinner },
            5 => new List<MealType> { MealType.Breakfast, MealType.MorningSnack, MealType.Lunch, MealType.AfternoonSnack, MealType.Dinner },
            6 => new List<MealType> { MealType.Breakfast, MealType.MorningSnack, MealType.Lunch, MealType.AfternoonSnack, MealType.Dinner, MealType.EveningSnack },
            _ => new List<MealType> { MealType.Breakfast, MealType.Lunch, MealType.Dinner }
        };

        // Remove skipped meals and replace with snacks to keep the same meal count
        if (skipBreakfast && meals.Contains(MealType.Breakfast))
        {
            meals.Remove(MealType.Breakfast);
            // Add a snack slot if not already present to keep calorie distribution
            if (!meals.Contains(MealType.MorningSnack))
                meals.Insert(0, MealType.MorningSnack);
        }

        if (skipLunch && meals.Contains(MealType.Lunch))
        {
            meals.Remove(MealType.Lunch);
            if (!meals.Contains(MealType.AfternoonSnack))
                meals.Add(MealType.AfternoonSnack);
        }

        return meals;
    }

    private static string FormatDietTypeName(DietType dietType) => dietType switch
    {
        DietType.GlutenFree => "Gluten-Free",
        DietType.DairyFree => "Dairy-Free",
        _ => dietType.ToString()
    };

    private static bool IsWeightChangeGoal(PrimaryNutritionGoal goal) => goal is
        PrimaryNutritionGoal.LoseWeight or
        PrimaryNutritionGoal.LoseBodyFat or
        PrimaryNutritionGoal.BodyRecomposition or
        PrimaryNutritionGoal.PrepareForCompetition or
        PrimaryNutritionGoal.BuildLeanMuscle or
        PrimaryNutritionGoal.AggressiveMuscleGain;

    // ===== Meal Templates =====
    // Each template defines a composed meal with named foods and serving multipliers.
    // ServingMultiplier is relative to the food's DefaultServingSize (1.0 = one standard serving).
    // All food names must exactly match entries in SeedData.Foods.

    private static readonly MealType[] SnackMealTypes =
        [MealType.MorningSnack, MealType.AfternoonSnack, MealType.EveningSnack];

    private static readonly MealTemplate[] _mealTemplates =
    [
        // ==================== BREAKFAST - General (20) ====================
        new("Oatmeal with Banana & Almonds",
            [MealType.Breakfast], MealCategory.General,
            [("Oats (dry)", 1.0), ("Banana", 0.75), ("Almonds", 1.0), ("Honey", 1.0)]),
        new("Scrambled Eggs with Toast",
            [MealType.Breakfast], MealCategory.General,
            [("Whole Egg", 2.0), ("Whole Wheat Bread", 2.0), ("Butter", 1.0)]),
        new("Greek Yogurt Parfait",
            [MealType.Breakfast], MealCategory.General,
            [("Greek Yogurt (plain, nonfat)", 1.0), ("Blueberries", 0.5), ("Granola", 1.0), ("Honey", 1.0)]),
        new("Protein Smoothie",
            [MealType.Breakfast], MealCategory.General,
            [("Whey Protein Powder", 1.0), ("Banana", 1.0), ("Almond Milk (unsweetened)", 0.75)]),
        new("Avocado Toast with Egg",
            [MealType.Breakfast], MealCategory.General,
            [("Whole Wheat Bread", 2.0), ("Avocado", 1.0), ("Whole Egg", 1.0)]),
        new("Breakfast Sandwich",
            [MealType.Breakfast], MealCategory.General,
            [("English Muffin", 1.0), ("Whole Egg", 2.0), ("Cheddar Cheese", 1.0)]),
        new("Bagel with Cream Cheese & Berries",
            [MealType.Breakfast], MealCategory.General,
            [("Bagel (plain)", 1.0), ("Cream Cheese", 1.0), ("Strawberries", 0.5)]),
        new("Overnight Oats with Peanut Butter",
            [MealType.Breakfast], MealCategory.General,
            [("Oats (dry)", 1.0), ("2% Milk", 0.5), ("Peanut Butter", 1.0), ("Honey", 1.0)]),
        new("Egg & Cheese Bagel",
            [MealType.Breakfast], MealCategory.General,
            [("Bagel (plain)", 1.0), ("Whole Egg", 2.0), ("Cheddar Cheese", 1.0)]),
        new("Banana Protein Pancakes",
            [MealType.Breakfast], MealCategory.General,
            [("Oats (dry)", 1.0), ("Whole Egg", 2.0), ("Banana", 1.0), ("Maple Syrup", 1.0)]),
        new("Turkey Sausage & Egg Plate",
            [MealType.Breakfast], MealCategory.General,
            [("Ground Turkey (cooked)", 0.5), ("Whole Egg", 2.0), ("Whole Wheat Bread", 1.0)]),
        new("Chocolate Protein Oatmeal",
            [MealType.Breakfast], MealCategory.General,
            [("Oats (dry)", 1.0), ("Whey Protein Powder", 0.5), ("Banana", 0.5), ("Almond Milk (unsweetened)", 0.5)]),
        new("Egg White Veggie Wrap",
            [MealType.Breakfast], MealCategory.General,
            [("Egg Whites", 4.0), ("Flour Tortilla", 1.0), ("Spinach (raw)", 1.0), ("Bell Pepper (red)", 0.5)]),
        new("Cottage Cheese & Pineapple Bowl",
            [MealType.Breakfast], MealCategory.General,
            [("Cottage Cheese (low-fat)", 1.0), ("Pineapple", 0.5), ("Granola", 0.5)]),
        new("Smoked Salmon Bagel",
            [MealType.Breakfast], MealCategory.General,
            [("Bagel (plain)", 1.0), ("Salmon (cooked)", 0.5), ("Cream Cheese", 1.0)]),
        new("French Toast with Berries",
            [MealType.Breakfast], MealCategory.General,
            [("White Bread", 3.0), ("Whole Egg", 2.0), ("2% Milk", 0.25), ("Strawberries", 0.5), ("Maple Syrup", 1.0)]),
        new("Granola & Milk Bowl",
            [MealType.Breakfast], MealCategory.General,
            [("Granola", 1.5), ("2% Milk", 0.75), ("Banana", 0.5)]),
        new("Breakfast Burrito",
            [MealType.Breakfast], MealCategory.General,
            [("Flour Tortilla", 1.5), ("Whole Egg", 2.0), ("Black Beans (cooked)", 0.5), ("Cheddar Cheese", 1.0), ("Salsa", 2.0)]),
        new("Yogurt & Fruit Smoothie Bowl",
            [MealType.Breakfast], MealCategory.General,
            [("Greek Yogurt (plain, nonfat)", 0.75), ("Banana", 0.75), ("Blueberries", 0.5), ("Granola", 0.75)]),
        new("Eggs Benedict Style Plate",
            [MealType.Breakfast], MealCategory.General,
            [("English Muffin", 1.0), ("Whole Egg", 2.0), ("Turkey Breast (cooked)", 0.5), ("Butter", 1.0)]),

        // ==================== BREAKFAST - Vegetarian (8) ====================
        new("Cottage Cheese & Fruit Bowl",
            [MealType.Breakfast], MealCategory.Vegetarian,
            [("Cottage Cheese (low-fat)", 1.0), ("Strawberries", 0.5), ("Chia Seeds", 1.0), ("Honey", 1.0)]),
        new("Veggie Egg Scramble",
            [MealType.Breakfast], MealCategory.Vegetarian,
            [("Whole Egg", 2.0), ("Spinach (raw)", 1.0), ("Bell Pepper (red)", 0.5), ("Feta Cheese", 1.0)]),
        new("Mushroom & Cheese Omelette",
            [MealType.Breakfast], MealCategory.Vegetarian,
            [("Whole Egg", 3.0), ("Mushrooms (white)", 1.0), ("Mozzarella Cheese", 1.0), ("Spinach (raw)", 1.0)]),
        new("Sweet Potato Hash with Eggs",
            [MealType.Breakfast], MealCategory.Vegetarian,
            [("Sweet Potato (baked)", 0.75), ("Whole Egg", 2.0), ("Bell Pepper (red)", 0.5), ("Olive Oil", 1.0)]),
        new("Ricotta Toast with Berries",
            [MealType.Breakfast], MealCategory.Vegetarian,
            [("Whole Wheat Bread", 2.0), ("Cottage Cheese (low-fat)", 0.75), ("Blueberries", 0.5), ("Honey", 1.0)]),
        new("Egg & Avocado Rice Bowl",
            [MealType.Breakfast], MealCategory.Vegetarian,
            [("Brown Rice (cooked)", 0.5), ("Whole Egg", 2.0), ("Avocado", 1.0), ("Soy Sauce", 1.0)]),
        new("Cheese & Spinach Quesadilla",
            [MealType.Breakfast], MealCategory.Vegetarian,
            [("Flour Tortilla", 1.5), ("Cheddar Cheese", 2.0), ("Spinach (raw)", 1.5), ("Salsa", 2.0)]),
        new("Yogurt Muesli Bowl",
            [MealType.Breakfast], MealCategory.Vegetarian,
            [("Yogurt (plain, whole milk)", 1.0), ("Oats (dry)", 0.5), ("Almonds", 0.75), ("Dates (Medjool)", 1.0)]),

        // ==================== BREAKFAST - Vegan (8) ====================
        new("Oatmeal with Berries & Seeds",
            [MealType.Breakfast], MealCategory.Vegan,
            [("Oats (dry)", 1.0), ("Blueberries", 0.5), ("Chia Seeds", 1.0), ("Almond Milk (unsweetened)", 0.75)]),
        new("Tofu Scramble with Toast",
            [MealType.Breakfast], MealCategory.Vegan,
            [("Tofu (firm)", 0.75), ("Spinach (raw)", 1.0), ("Whole Wheat Bread", 2.0), ("Olive Oil", 1.0)]),
        new("Peanut Butter Banana Toast",
            [MealType.Breakfast], MealCategory.Vegan,
            [("Whole Wheat Bread", 2.0), ("Peanut Butter", 1.0), ("Banana", 0.75)]),
        new("Chia Seed Pudding",
            [MealType.Breakfast], MealCategory.Vegan,
            [("Chia Seeds", 2.0), ("Almond Milk (unsweetened)", 0.75), ("Mango", 0.5), ("Maple Syrup", 1.0)]),
        new("Tropical Smoothie Bowl",
            [MealType.Breakfast], MealCategory.Vegan,
            [("Banana", 1.0), ("Mango", 0.5), ("Oat Milk", 0.5), ("Granola", 0.75)]),
        new("Avocado & Bean Toast",
            [MealType.Breakfast], MealCategory.Vegan,
            [("Whole Wheat Bread", 2.0), ("Avocado", 1.0), ("Black Beans (cooked)", 0.5), ("Hot Sauce", 1.0)]),
        new("Overnight Oats with Almond Butter",
            [MealType.Breakfast], MealCategory.Vegan,
            [("Oats (dry)", 1.0), ("Almond Milk (unsweetened)", 0.75), ("Almond Butter", 1.0), ("Banana", 0.5)]),
        new("Tempeh Breakfast Bowl",
            [MealType.Breakfast], MealCategory.Vegan,
            [("Tempeh", 0.75), ("Sweet Potato (baked)", 0.5), ("Spinach (raw)", 1.5), ("Olive Oil", 1.0)]),

        // ==================== BREAKFAST - Keto (8) ====================
        new("Egg & Cheese Scramble with Avocado",
            [MealType.Breakfast], MealCategory.Keto,
            [("Whole Egg", 3.0), ("Cheddar Cheese", 1.5), ("Avocado", 1.0), ("Butter", 1.0)]),
        new("Cottage Cheese with Nuts",
            [MealType.Breakfast], MealCategory.Keto,
            [("Cottage Cheese (low-fat)", 1.0), ("Almonds", 1.5), ("Walnuts", 1.0)]),
        new("Bacon-Style Egg Cups",
            [MealType.Breakfast], MealCategory.Keto,
            [("Whole Egg", 3.0), ("Cheddar Cheese", 2.0), ("Mushrooms (white)", 1.0), ("Butter", 1.0)]),
        new("Salmon & Cream Cheese Plate",
            [MealType.Breakfast], MealCategory.Keto,
            [("Salmon (cooked)", 0.5), ("Cream Cheese", 2.0), ("Cucumber", 0.5), ("Olive Oil", 1.0)]),
        new("Keto Avocado Egg Boats",
            [MealType.Breakfast], MealCategory.Keto,
            [("Avocado", 1.5), ("Whole Egg", 2.0), ("Cheddar Cheese", 1.0)]),
        new("Nut & Seed Yogurt Bowl",
            [MealType.Breakfast], MealCategory.Keto,
            [("Greek Yogurt (plain, nonfat)", 0.75), ("Almonds", 1.0), ("Chia Seeds", 1.0), ("Coconut Oil", 0.5)]),
        new("Mushroom & Cheese Frittata",
            [MealType.Breakfast], MealCategory.Keto,
            [("Whole Egg", 3.0), ("Mushrooms (white)", 1.5), ("Parmesan Cheese", 2.0), ("Olive Oil", 1.0)]),
        new("Pork & Egg Breakfast Plate",
            [MealType.Breakfast], MealCategory.Keto,
            [("Pork Chop (cooked)", 0.5), ("Whole Egg", 2.0), ("Avocado", 1.0), ("Butter", 1.0)]),

        // ==================== LUNCH - General (20) ====================
        new("Grilled Chicken Rice Bowl",
            [MealType.Lunch], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Broccoli", 1.0)]),
        new("Turkey Wrap",
            [MealType.Lunch], MealCategory.General,
            [("Turkey Breast (cooked)", 1.0), ("Flour Tortilla", 1.5), ("Spinach (raw)", 1.0), ("Tomato", 0.5)]),
        new("Salmon Quinoa Bowl",
            [MealType.Lunch], MealCategory.General,
            [("Salmon (cooked)", 0.75), ("Quinoa (cooked)", 0.75), ("Asparagus", 1.0)]),
        new("Tuna Sandwich",
            [MealType.Lunch], MealCategory.General,
            [("Tuna (canned in water)", 1.0), ("Whole Wheat Bread", 2.0), ("Romaine Lettuce", 1.0)]),
        new("Chicken & Sweet Potato Plate",
            [MealType.Lunch], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Sweet Potato (baked)", 1.0), ("Green Beans", 1.0)]),
        new("Shrimp & Rice Bowl",
            [MealType.Lunch], MealCategory.General,
            [("Shrimp (cooked)", 1.5), ("Brown Rice (cooked)", 0.75), ("Bell Pepper (red)", 0.5), ("Olive Oil", 1.0)]),
        new("Chicken Burrito Bowl",
            [MealType.Lunch], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Brown Rice (cooked)", 0.5), ("Black Beans (cooked)", 0.5), ("Salsa", 2.0), ("Avocado", 0.5)]),
        new("Turkey & Avocado Sandwich",
            [MealType.Lunch], MealCategory.General,
            [("Turkey Breast (cooked)", 1.0), ("Whole Wheat Bread", 2.0), ("Avocado", 0.75), ("Tomato", 0.5)]),
        new("Steak Fajita Bowl",
            [MealType.Lunch], MealCategory.General,
            [("Sirloin Steak (cooked)", 0.75), ("Brown Rice (cooked)", 0.5), ("Bell Pepper (red)", 1.0), ("Onion", 0.5), ("Salsa", 2.0)]),
        new("Chicken Pasta Salad",
            [MealType.Lunch], MealCategory.General,
            [("Chicken Breast (cooked)", 0.75), ("Pasta (cooked)", 0.75), ("Tomato", 0.5), ("Olive Oil", 1.0)]),
        new("Cod & Potato Plate",
            [MealType.Lunch], MealCategory.General,
            [("Cod (cooked)", 1.0), ("Potato (baked)", 1.0), ("Peas (green)", 0.75)]),
        new("Ground Turkey Taco Bowl",
            [MealType.Lunch], MealCategory.General,
            [("Ground Turkey (cooked)", 1.0), ("Brown Rice (cooked)", 0.5), ("Black Beans (cooked)", 0.5), ("Cheddar Cheese", 1.0), ("Salsa", 2.0)]),
        new("Chicken Caesar Wrap",
            [MealType.Lunch], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Flour Tortilla", 1.5), ("Romaine Lettuce", 1.5), ("Parmesan Cheese", 1.5)]),
        new("Salmon & Couscous Plate",
            [MealType.Lunch], MealCategory.General,
            [("Salmon (cooked)", 0.75), ("Couscous (cooked)", 0.75), ("Cucumber", 0.5), ("Olive Oil", 1.0)]),
        new("Beef & Broccoli Rice",
            [MealType.Lunch], MealCategory.General,
            [("Ground Beef 90% Lean", 1.0), ("Brown Rice (cooked)", 0.75), ("Broccoli", 1.0), ("Soy Sauce", 2.0)]),
        new("Pork Stir-Fry Bowl",
            [MealType.Lunch], MealCategory.General,
            [("Pork Tenderloin (cooked)", 1.0), ("White Rice (cooked)", 0.75), ("Bell Pepper (red)", 0.5), ("Mushrooms (white)", 1.0)]),
        new("Tuna Melt Sandwich",
            [MealType.Lunch], MealCategory.General,
            [("Tuna (canned in water)", 1.0), ("Whole Wheat Bread", 2.0), ("Cheddar Cheese", 1.0)]),
        new("Chicken Thigh & Quinoa Bowl",
            [MealType.Lunch], MealCategory.General,
            [("Chicken Thigh (cooked)", 1.0), ("Quinoa (cooked)", 0.75), ("Spinach (raw)", 1.5), ("Olive Oil", 1.0)]),
        new("Shrimp Tacos",
            [MealType.Lunch], MealCategory.General,
            [("Shrimp (cooked)", 1.5), ("Corn Tortilla", 3.0), ("Cabbage", 0.5), ("Avocado", 0.5), ("Salsa", 2.0)]),
        new("BBQ Chicken Sweet Potato",
            [MealType.Lunch], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Sweet Potato (baked)", 1.0), ("BBQ Sauce", 2.0), ("Corn (sweet)", 0.5)]),

        // ==================== LUNCH - Vegetarian (10) ====================
        new("Bean & Rice Burrito Bowl",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Black Beans (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Bell Pepper (red)", 0.5), ("Salsa", 2.0), ("Avocado", 0.5)]),
        new("Chickpea Salad with Feta",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Chickpeas (cooked)", 1.0), ("Cucumber", 0.5), ("Tomato", 0.5), ("Feta Cheese", 1.0), ("Olive Oil", 1.0)]),
        new("Egg Salad Sandwich",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Whole Egg", 3.0), ("Whole Wheat Bread", 2.0), ("Romaine Lettuce", 1.0)]),
        new("Caprese Pasta Salad",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Pasta (cooked)", 1.0), ("Mozzarella Cheese", 2.0), ("Tomato", 0.75), ("Olive Oil", 1.0)]),
        new("Greek Salad with Quinoa",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Quinoa (cooked)", 0.75), ("Cucumber", 0.75), ("Tomato", 0.5), ("Feta Cheese", 1.5), ("Olive Oil", 1.0)]),
        new("Mushroom & Bean Rice Bowl",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Mushrooms (white)", 1.5), ("Kidney Beans (cooked)", 0.75), ("Brown Rice (cooked)", 0.75), ("Olive Oil", 1.0)]),
        new("Veggie & Hummus Wrap",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Flour Tortilla", 1.5), ("Hummus", 3.0), ("Bell Pepper (red)", 0.5), ("Cucumber", 0.5), ("Spinach (raw)", 1.0)]),
        new("Eggplant Parmesan Plate",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Eggplant", 1.5), ("Mozzarella Cheese", 2.0), ("Tomato", 0.5), ("Olive Oil", 1.0)]),
        new("Sweet Potato & Black Bean Bowl",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Sweet Potato (baked)", 1.0), ("Black Beans (cooked)", 0.75), ("Avocado", 0.5), ("Salsa", 2.0)]),
        new("Cheese & Bean Quesadilla",
            [MealType.Lunch], MealCategory.Vegetarian,
            [("Flour Tortilla", 2.0), ("Cheddar Cheese", 2.0), ("Black Beans (cooked)", 0.75), ("Salsa", 2.0)]),

        // ==================== LUNCH - Vegan (10) ====================
        new("Lentil & Rice Bowl",
            [MealType.Lunch], MealCategory.Vegan,
            [("Lentils (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Spinach (raw)", 1.0), ("Tomato", 0.5)]),
        new("Tofu Veggie Stir-Fry",
            [MealType.Lunch], MealCategory.Vegan,
            [("Tofu (firm)", 1.0), ("Brown Rice (cooked)", 0.75), ("Bell Pepper (red)", 0.5), ("Broccoli", 1.0)]),
        new("Chickpea & Avocado Wrap",
            [MealType.Lunch], MealCategory.Vegan,
            [("Chickpeas (cooked)", 1.0), ("Flour Tortilla", 1.5), ("Avocado", 0.5), ("Spinach (raw)", 1.0)]),
        new("Quinoa & Black Bean Salad",
            [MealType.Lunch], MealCategory.Vegan,
            [("Quinoa (cooked)", 1.0), ("Black Beans (cooked)", 0.75), ("Corn (sweet)", 0.5), ("Tomato", 0.5), ("Olive Oil", 1.0)]),
        new("Tempeh Teriyaki Bowl",
            [MealType.Lunch], MealCategory.Vegan,
            [("Tempeh", 1.0), ("Brown Rice (cooked)", 0.75), ("Broccoli", 1.0), ("Soy Sauce", 2.0)]),
        new("Mediterranean Chickpea Bowl",
            [MealType.Lunch], MealCategory.Vegan,
            [("Chickpeas (cooked)", 1.0), ("Couscous (cooked)", 0.75), ("Cucumber", 0.5), ("Tomato", 0.5), ("Olive Oil", 1.0)]),
        new("Peanut Noodle Bowl",
            [MealType.Lunch], MealCategory.Vegan,
            [("Pasta (cooked)", 1.0), ("Peanut Butter", 1.0), ("Carrots", 0.75), ("Cabbage", 0.5), ("Soy Sauce", 2.0)]),
        new("Sweet Potato & Lentil Stew",
            [MealType.Lunch], MealCategory.Vegan,
            [("Sweet Potato (baked)", 1.0), ("Lentils (cooked)", 1.0), ("Spinach (raw)", 1.5), ("Tomato", 0.5)]),
        new("Tofu & Quinoa Power Bowl",
            [MealType.Lunch], MealCategory.Vegan,
            [("Tofu (firm)", 1.0), ("Quinoa (cooked)", 0.75), ("Avocado", 0.5), ("Kale (raw)", 1.5)]),
        new("Bean & Corn Burrito",
            [MealType.Lunch], MealCategory.Vegan,
            [("Black Beans (cooked)", 1.0), ("Flour Tortilla", 1.5), ("Corn (sweet)", 0.5), ("Avocado", 0.5), ("Salsa", 2.0)]),

        // ==================== LUNCH - Keto (8) ====================
        new("Chicken Caesar Salad",
            [MealType.Lunch], MealCategory.Keto,
            [("Chicken Breast (cooked)", 1.0), ("Romaine Lettuce", 2.0), ("Parmesan Cheese", 2.0), ("Olive Oil", 1.5)]),
        new("Steak & Avocado Salad",
            [MealType.Lunch], MealCategory.Keto,
            [("Sirloin Steak (cooked)", 0.75), ("Romaine Lettuce", 2.0), ("Avocado", 1.0), ("Olive Oil", 1.0)]),
        new("Tuna Lettuce Wraps",
            [MealType.Lunch], MealCategory.Keto,
            [("Tuna (canned in water)", 1.5), ("Romaine Lettuce", 2.0), ("Avocado", 1.0), ("Olive Oil", 1.0)]),
        new("Salmon & Avocado Plate",
            [MealType.Lunch], MealCategory.Keto,
            [("Salmon (cooked)", 0.75), ("Avocado", 1.5), ("Cucumber", 0.75), ("Olive Oil", 1.0)]),
        new("Chicken Thigh Salad",
            [MealType.Lunch], MealCategory.Keto,
            [("Chicken Thigh (cooked)", 1.0), ("Spinach (raw)", 2.0), ("Avocado", 1.0), ("Almonds", 1.0)]),
        new("Egg & Cheese Stuffed Peppers",
            [MealType.Lunch], MealCategory.Keto,
            [("Bell Pepper (red)", 1.5), ("Whole Egg", 3.0), ("Cheddar Cheese", 2.0), ("Butter", 1.0)]),
        new("Shrimp & Avocado Bowl",
            [MealType.Lunch], MealCategory.Keto,
            [("Shrimp (cooked)", 2.0), ("Avocado", 1.5), ("Romaine Lettuce", 1.5), ("Olive Oil", 1.5)]),
        new("Pork & Cauliflower Plate",
            [MealType.Lunch], MealCategory.Keto,
            [("Pork Chop (cooked)", 1.0), ("Cauliflower", 1.5), ("Butter", 1.5)]),

        // ==================== DINNER - General (25) ====================
        new("Steak with Sweet Potato & Green Beans",
            [MealType.Dinner], MealCategory.General,
            [("Sirloin Steak (cooked)", 1.0), ("Sweet Potato (baked)", 1.0), ("Green Beans", 1.0)]),
        new("Baked Salmon with Rice & Broccoli",
            [MealType.Dinner], MealCategory.General,
            [("Salmon (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Broccoli", 1.0)]),
        new("Chicken Stir-Fry with Rice",
            [MealType.Dinner], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Bell Pepper (red)", 0.5), ("Mushrooms (white)", 1.0)]),
        new("Pork Tenderloin with Potato & Asparagus",
            [MealType.Dinner], MealCategory.General,
            [("Pork Tenderloin (cooked)", 1.0), ("Potato (baked)", 1.0), ("Asparagus", 1.0)]),
        new("Turkey Pasta Bolognese",
            [MealType.Dinner], MealCategory.General,
            [("Ground Turkey (cooked)", 1.0), ("Whole Wheat Pasta (cooked)", 0.75), ("Tomato", 0.5)]),
        new("Fish Tacos",
            [MealType.Dinner], MealCategory.General,
            [("Tilapia (cooked)", 1.0), ("Corn Tortilla", 3.0), ("Cabbage", 0.5), ("Avocado", 0.5)]),
        new("Shrimp & Vegetable Pasta",
            [MealType.Dinner], MealCategory.General,
            [("Shrimp (cooked)", 1.5), ("Pasta (cooked)", 0.75), ("Zucchini", 0.5), ("Tomato", 0.5)]),
        new("Beef Stir-Fry with Noodles",
            [MealType.Dinner], MealCategory.General,
            [("Ground Beef 90% Lean", 1.0), ("Pasta (cooked)", 0.75), ("Bell Pepper (red)", 0.5), ("Broccoli", 1.0), ("Soy Sauce", 2.0)]),
        new("Chicken Fajita Plate",
            [MealType.Dinner], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Flour Tortilla", 2.0), ("Bell Pepper (red)", 1.0), ("Onion", 0.5), ("Guacamole", 2.0)]),
        new("Baked Cod with Potato & Peas",
            [MealType.Dinner], MealCategory.General,
            [("Cod (cooked)", 1.0), ("Potato (baked)", 1.0), ("Peas (green)", 1.0), ("Butter", 1.0)]),
        new("BBQ Chicken with Corn & Coleslaw",
            [MealType.Dinner], MealCategory.General,
            [("Chicken Thigh (cooked)", 1.0), ("Corn (sweet)", 1.0), ("Cabbage", 1.0), ("BBQ Sauce", 2.0)]),
        new("Pork Chop with Rice & Brussels Sprouts",
            [MealType.Dinner], MealCategory.General,
            [("Pork Chop (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Brussels Sprouts", 1.0)]),
        new("Salmon Teriyaki Bowl",
            [MealType.Dinner], MealCategory.General,
            [("Salmon (cooked)", 1.0), ("White Rice (cooked)", 0.75), ("Broccoli", 1.0), ("Soy Sauce", 2.0)]),
        new("Turkey Meatball Pasta",
            [MealType.Dinner], MealCategory.General,
            [("Ground Turkey (cooked)", 1.0), ("Pasta (cooked)", 1.0), ("Tomato", 0.5), ("Parmesan Cheese", 1.0)]),
        new("Grilled Chicken & Quinoa Salad",
            [MealType.Dinner], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Quinoa (cooked)", 0.75), ("Cucumber", 0.5), ("Tomato", 0.5), ("Olive Oil", 1.0)]),
        new("Beef Burrito Bowl",
            [MealType.Dinner], MealCategory.General,
            [("Ground Beef 90% Lean", 1.0), ("Brown Rice (cooked)", 0.75), ("Black Beans (cooked)", 0.5), ("Cheddar Cheese", 1.0), ("Salsa", 2.0)]),
        new("Tilapia with Couscous & Vegetables",
            [MealType.Dinner], MealCategory.General,
            [("Tilapia (cooked)", 1.0), ("Couscous (cooked)", 0.75), ("Zucchini", 0.5), ("Tomato", 0.5)]),
        new("Chicken Thigh with Sweet Potato & Kale",
            [MealType.Dinner], MealCategory.General,
            [("Chicken Thigh (cooked)", 1.0), ("Sweet Potato (baked)", 1.0), ("Kale (raw)", 2.0), ("Olive Oil", 1.0)]),
        new("Shrimp Fried Rice",
            [MealType.Dinner], MealCategory.General,
            [("Shrimp (cooked)", 1.5), ("White Rice (cooked)", 1.0), ("Peas (green)", 0.5), ("Whole Egg", 1.0), ("Soy Sauce", 2.0)]),
        new("Steak & Baked Potato",
            [MealType.Dinner], MealCategory.General,
            [("Sirloin Steak (cooked)", 1.0), ("Potato (baked)", 1.0), ("Butter", 1.0), ("Broccoli", 1.0)]),
        new("Honey Garlic Chicken & Rice",
            [MealType.Dinner], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Green Beans", 1.0), ("Honey", 1.0)]),
        new("Pork Stir-Fry with Vegetables",
            [MealType.Dinner], MealCategory.General,
            [("Pork Tenderloin (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Bell Pepper (red)", 0.5), ("Mushrooms (white)", 1.0), ("Soy Sauce", 2.0)]),
        new("Beef & Bean Chili with Rice",
            [MealType.Dinner], MealCategory.General,
            [("Ground Beef 80% Lean", 1.0), ("Kidney Beans (cooked)", 0.75), ("Tomato", 0.5), ("Brown Rice (cooked)", 0.5)]),
        new("Tuna Pasta Bake",
            [MealType.Dinner], MealCategory.General,
            [("Tuna (canned in water)", 1.5), ("Pasta (cooked)", 1.0), ("Mozzarella Cheese", 1.5), ("Tomato", 0.5)]),
        new("Chicken Parmesan with Pasta",
            [MealType.Dinner], MealCategory.General,
            [("Chicken Breast (cooked)", 1.0), ("Pasta (cooked)", 0.75), ("Mozzarella Cheese", 1.5), ("Tomato", 0.5)]),

        // ==================== DINNER - Vegetarian (12) ====================
        new("Veggie Pasta with Mozzarella",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Whole Wheat Pasta (cooked)", 1.0), ("Zucchini", 0.5), ("Tomato", 0.5), ("Mozzarella Cheese", 1.5), ("Olive Oil", 1.0)]),
        new("Egg Fried Rice with Vegetables",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Brown Rice (cooked)", 1.0), ("Whole Egg", 2.0), ("Peas (green)", 0.5), ("Carrots", 0.5)]),
        new("Black Bean Quesadilla",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Black Beans (cooked)", 1.0), ("Flour Tortilla", 2.0), ("Cheddar Cheese", 1.5), ("Salsa", 2.0)]),
        new("Mushroom Risotto",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("White Rice (cooked)", 1.0), ("Mushrooms (white)", 2.0), ("Parmesan Cheese", 2.0), ("Butter", 1.0), ("Olive Oil", 1.0)]),
        new("Spinach & Cheese Stuffed Potato",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Potato (baked)", 1.5), ("Spinach (raw)", 2.0), ("Cheddar Cheese", 2.0), ("Butter", 1.0)]),
        new("Vegetable Pasta Primavera",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Pasta (cooked)", 1.0), ("Zucchini", 0.5), ("Bell Pepper (red)", 0.5), ("Mushrooms (white)", 1.0), ("Parmesan Cheese", 1.5)]),
        new("Bean & Cheese Enchiladas",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Kidney Beans (cooked)", 1.0), ("Corn Tortilla", 3.0), ("Cheddar Cheese", 2.0), ("Salsa", 3.0)]),
        new("Chickpea & Potato Curry",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Chickpeas (cooked)", 1.0), ("Potato (baked)", 1.0), ("Tomato", 0.5), ("Olive Oil", 1.0)]),
        new("Eggplant & Lentil Bake",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Eggplant", 1.5), ("Lentils (cooked)", 1.0), ("Tomato", 0.75), ("Mozzarella Cheese", 1.5)]),
        new("Broccoli & Cheese Rice Casserole",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Brown Rice (cooked)", 1.0), ("Broccoli", 1.5), ("Cheddar Cheese", 2.0), ("2% Milk", 0.25)]),
        new("Egg & Vegetable Stir-Fry",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Whole Egg", 3.0), ("Brown Rice (cooked)", 0.75), ("Bell Pepper (red)", 0.5), ("Mushrooms (white)", 1.0), ("Soy Sauce", 2.0)]),
        new("Cauliflower Mac & Cheese",
            [MealType.Dinner], MealCategory.Vegetarian,
            [("Pasta (cooked)", 1.0), ("Cauliflower", 1.0), ("Cheddar Cheese", 2.0), ("2% Milk", 0.25)]),

        // ==================== DINNER - Vegan (12) ====================
        new("Tempeh Buddha Bowl",
            [MealType.Dinner], MealCategory.Vegan,
            [("Tempeh", 1.0), ("Quinoa (cooked)", 0.75), ("Kale (raw)", 1.5), ("Avocado", 0.75)]),
        new("Chickpea & Sweet Potato Stew",
            [MealType.Dinner], MealCategory.Vegan,
            [("Chickpeas (cooked)", 1.0), ("Sweet Potato (baked)", 1.0), ("Spinach (raw)", 1.5), ("Tomato", 0.5)]),
        new("Black Bean Tacos",
            [MealType.Dinner], MealCategory.Vegan,
            [("Black Beans (cooked)", 1.0), ("Corn Tortilla", 3.0), ("Cabbage", 0.5), ("Avocado", 0.5), ("Salsa", 2.0)]),
        new("Lentil & Vegetable Curry",
            [MealType.Dinner], MealCategory.Vegan,
            [("Lentils (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Cauliflower", 1.0), ("Spinach (raw)", 1.0)]),
        new("Tofu & Broccoli Stir-Fry",
            [MealType.Dinner], MealCategory.Vegan,
            [("Tofu (firm)", 1.0), ("Brown Rice (cooked)", 0.75), ("Broccoli", 1.5), ("Soy Sauce", 2.0), ("Olive Oil", 1.0)]),
        new("Vegan Burrito Bowl",
            [MealType.Dinner], MealCategory.Vegan,
            [("Black Beans (cooked)", 1.0), ("Brown Rice (cooked)", 0.75), ("Corn (sweet)", 0.5), ("Avocado", 1.0), ("Salsa", 2.0)]),
        new("Peanut Tofu Noodles",
            [MealType.Dinner], MealCategory.Vegan,
            [("Tofu (firm)", 1.0), ("Pasta (cooked)", 0.75), ("Peanut Butter", 1.0), ("Carrots", 0.5), ("Soy Sauce", 2.0)]),
        new("Lentil Bolognese",
            [MealType.Dinner], MealCategory.Vegan,
            [("Lentils (cooked)", 1.0), ("Whole Wheat Pasta (cooked)", 1.0), ("Tomato", 0.75), ("Mushrooms (white)", 1.0)]),
        new("Kidney Bean & Rice Plate",
            [MealType.Dinner], MealCategory.Vegan,
            [("Kidney Beans (cooked)", 1.0), ("White Rice (cooked)", 0.75), ("Bell Pepper (red)", 0.5), ("Onion", 0.5), ("Olive Oil", 1.0)]),
        new("Stuffed Bell Peppers",
            [MealType.Dinner], MealCategory.Vegan,
            [("Bell Pepper (red)", 2.0), ("Quinoa (cooked)", 0.75), ("Black Beans (cooked)", 0.75), ("Tomato", 0.5)]),
        new("Tempeh & Sweet Potato Bowl",
            [MealType.Dinner], MealCategory.Vegan,
            [("Tempeh", 1.0), ("Sweet Potato (baked)", 1.0), ("Kale (raw)", 2.0), ("Tahini", 1.0)]),
        new("Chickpea Pasta with Vegetables",
            [MealType.Dinner], MealCategory.Vegan,
            [("Chickpeas (cooked)", 1.0), ("Pasta (cooked)", 0.75), ("Zucchini", 0.5), ("Tomato", 0.5), ("Olive Oil", 1.0)]),

        // ==================== DINNER - Keto (10) ====================
        new("Steak with Cauliflower Mash",
            [MealType.Dinner], MealCategory.Keto,
            [("Sirloin Steak (cooked)", 1.0), ("Cauliflower", 1.5), ("Butter", 1.5), ("Olive Oil", 1.0)]),
        new("Salmon with Asparagus & Avocado",
            [MealType.Dinner], MealCategory.Keto,
            [("Salmon (cooked)", 1.0), ("Asparagus", 1.5), ("Avocado", 1.0)]),
        new("Chicken Thigh with Zucchini & Mushrooms",
            [MealType.Dinner], MealCategory.Keto,
            [("Chicken Thigh (cooked)", 1.0), ("Zucchini", 1.0), ("Mushrooms (white)", 1.0), ("Olive Oil", 1.0)]),
        new("Pork Chop with Broccoli & Butter",
            [MealType.Dinner], MealCategory.Keto,
            [("Pork Chop (cooked)", 1.0), ("Broccoli", 1.5), ("Butter", 2.0), ("Olive Oil", 1.0)]),
        new("Shrimp & Avocado Plate",
            [MealType.Dinner], MealCategory.Keto,
            [("Shrimp (cooked)", 2.0), ("Avocado", 1.5), ("Asparagus", 1.0), ("Olive Oil", 1.5)]),
        new("Beef Patty with Cheese & Salad",
            [MealType.Dinner], MealCategory.Keto,
            [("Ground Beef 80% Lean", 1.0), ("Cheddar Cheese", 2.0), ("Romaine Lettuce", 2.0), ("Avocado", 1.0)]),
        new("Cod with Butter Sauce & Vegetables",
            [MealType.Dinner], MealCategory.Keto,
            [("Cod (cooked)", 1.0), ("Butter", 2.0), ("Asparagus", 1.5), ("Mushrooms (white)", 1.0)]),
        new("Chicken & Bacon Salad Bowl",
            [MealType.Dinner], MealCategory.Keto,
            [("Chicken Breast (cooked)", 1.0), ("Romaine Lettuce", 2.0), ("Avocado", 1.0), ("Cheddar Cheese", 1.5), ("Olive Oil", 1.0)]),
        new("Salmon & Cream Cheese Stuffed Peppers",
            [MealType.Dinner], MealCategory.Keto,
            [("Salmon (cooked)", 0.75), ("Bell Pepper (red)", 1.5), ("Cream Cheese", 2.0), ("Olive Oil", 1.0)]),
        new("Steak & Mushroom Plate",
            [MealType.Dinner], MealCategory.Keto,
            [("Sirloin Steak (cooked)", 1.0), ("Mushrooms (white)", 2.0), ("Butter", 2.0), ("Spinach (raw)", 2.0)]),

        // ==================== SNACKS - General (12) ====================
        new("Apple with Peanut Butter",
            SnackMealTypes, MealCategory.General,
            [("Apple", 1.0), ("Peanut Butter", 1.0)]),
        new("Protein Bar",
            SnackMealTypes, MealCategory.General,
            [("Protein Bar", 1.0)]),
        new("Trail Mix & Banana",
            SnackMealTypes, MealCategory.General,
            [("Trail Mix", 1.0), ("Banana", 0.5)]),
        new("Hummus & Veggie Sticks",
            SnackMealTypes, MealCategory.General,
            [("Hummus", 2.0), ("Carrots", 1.0), ("Cucumber", 0.5)]),
        new("Greek Yogurt & Berries",
            SnackMealTypes, MealCategory.General,
            [("Greek Yogurt (plain, nonfat)", 0.75), ("Raspberries", 0.5)]),
        new("Banana & Almond Butter",
            SnackMealTypes, MealCategory.General,
            [("Banana", 1.0), ("Almond Butter", 1.0)]),
        new("Cottage Cheese & Pineapple",
            SnackMealTypes, MealCategory.General,
            [("Cottage Cheese (low-fat)", 0.75), ("Pineapple", 0.5)]),
        new("Granola Bar & Milk",
            SnackMealTypes, MealCategory.General,
            [("Granola Bar", 1.0), ("2% Milk", 0.5)]),
        new("Hard Boiled Eggs",
            SnackMealTypes, MealCategory.General,
            [("Whole Egg", 2.0)]),
        new("Beef Jerky & Orange",
            SnackMealTypes, MealCategory.General,
            [("Beef Jerky", 1.0), ("Orange", 1.0)]),
        new("Yogurt & Honey",
            SnackMealTypes, MealCategory.General,
            [("Yogurt (plain, whole milk)", 0.75), ("Honey", 1.0)]),
        new("Chocolate Protein Shake",
            SnackMealTypes, MealCategory.General,
            [("Whey Protein Powder", 1.0), ("Chocolate Milk", 0.5)]),

        // ==================== SNACKS - Vegetarian (6) ====================
        new("Cheese & Crackers",
            SnackMealTypes, MealCategory.Vegetarian,
            [("Cheddar Cheese", 1.5), ("Crackers (whole wheat)", 1.0)]),
        new("Yogurt with Granola",
            SnackMealTypes, MealCategory.Vegetarian,
            [("Yogurt (plain, whole milk)", 0.75), ("Granola", 0.75)]),
        new("Caprese Bites",
            SnackMealTypes, MealCategory.Vegetarian,
            [("Mozzarella Cheese", 2.0), ("Tomato", 0.5), ("Olive Oil", 0.5)]),
        new("Cottage Cheese & Berries",
            SnackMealTypes, MealCategory.Vegetarian,
            [("Cottage Cheese (low-fat)", 1.0), ("Strawberries", 0.5), ("Blueberries", 0.25)]),
        new("Cheese & Apple Slices",
            SnackMealTypes, MealCategory.Vegetarian,
            [("Cheddar Cheese", 1.5), ("Apple", 0.5)]),
        new("Egg & Avocado Snack Plate",
            SnackMealTypes, MealCategory.Vegetarian,
            [("Whole Egg", 1.0), ("Avocado", 0.75)]),

        // ==================== SNACKS - Vegan (8) ====================
        new("Rice Cakes with Almond Butter",
            SnackMealTypes, MealCategory.Vegan,
            [("Rice Cakes", 2.0), ("Almond Butter", 1.0)]),
        new("Dark Chocolate & Almonds",
            SnackMealTypes, MealCategory.Vegan,
            [("Dark Chocolate (70%)", 1.0), ("Almonds", 1.0)]),
        new("Edamame Bowl",
            SnackMealTypes, MealCategory.Vegan,
            [("Edamame (shelled)", 1.5)]),
        new("Dates & Cashews",
            SnackMealTypes, MealCategory.Vegan,
            [("Dates (Medjool)", 2.0), ("Cashews", 1.0)]),
        new("Hummus & Crackers",
            SnackMealTypes, MealCategory.Vegan,
            [("Hummus", 3.0), ("Crackers (whole wheat)", 1.0)]),
        new("Fruit & Nut Trail Mix",
            SnackMealTypes, MealCategory.Vegan,
            [("Trail Mix", 1.5), ("Dried Cranberries", 0.5)]),
        new("PB & Banana Rice Cakes",
            SnackMealTypes, MealCategory.Vegan,
            [("Rice Cakes", 2.0), ("Peanut Butter", 1.0), ("Banana", 0.5)]),
        new("Mango & Cashew Bowl",
            SnackMealTypes, MealCategory.Vegan,
            [("Mango", 0.75), ("Cashews", 1.0)]),

        // ==================== SNACKS - Keto (6) ====================
        new("Cheese & Mixed Nuts",
            SnackMealTypes, MealCategory.Keto,
            [("Cheddar Cheese", 2.0), ("Mixed Nuts", 1.0)]),
        new("Cottage Cheese with Walnuts",
            SnackMealTypes, MealCategory.Keto,
            [("Cottage Cheese (low-fat)", 0.75), ("Walnuts", 1.0)]),
        new("Avocado & Sunflower Seeds",
            SnackMealTypes, MealCategory.Keto,
            [("Avocado", 1.0), ("Sunflower Seeds", 1.0)]),
        new("Dark Chocolate & Macadamia",
            SnackMealTypes, MealCategory.Keto,
            [("Dark Chocolate (70%)", 0.75), ("Mixed Nuts", 1.0)]),
        new("Celery with Cream Cheese",
            SnackMealTypes, MealCategory.Keto,
            [("Celery", 3.0), ("Cream Cheese", 2.0)]),
        new("Pumpkin Seeds & Almonds",
            SnackMealTypes, MealCategory.Keto,
            [("Pumpkin Seeds", 1.0), ("Almonds", 1.0)]),

        // ==================== PRE/POST WORKOUT (6) ====================
        new("Pre-Workout Fuel",
            [MealType.PreWorkout], MealCategory.General,
            [("Banana", 1.0), ("Peanut Butter", 0.5), ("Rice Cakes", 1.0)]),
        new("Post-Workout Recovery Shake",
            [MealType.PostWorkout], MealCategory.General,
            [("Whey Protein Powder", 1.0), ("Banana", 1.0)]),
        new("Oatmeal Pre-Workout Bowl",
            [MealType.PreWorkout], MealCategory.General,
            [("Oats (dry)", 1.0), ("Banana", 0.75), ("Honey", 1.0)]),
        new("Post-Workout Protein & Rice",
            [MealType.PostWorkout], MealCategory.General,
            [("Whey Protein Powder", 1.0), ("White Rice (cooked)", 0.5), ("Banana", 0.5)]),
        new("Vegan Pre-Workout Snack",
            [MealType.PreWorkout], MealCategory.Vegan,
            [("Banana", 1.0), ("Oats (dry)", 0.5), ("Almond Butter", 0.5)]),
        new("Vegan Post-Workout Shake",
            [MealType.PostWorkout], MealCategory.Vegan,
            [("Banana", 1.0), ("Soy Milk", 1.0), ("Peanut Butter", 0.5)]),
    ];

    /// <summary>
    /// Post-generation correction pass: adjusts item servings across an entire day
    /// to bring actual macros within ±15g of targets. Uses group-based proportional
    /// scaling followed by single-item fine-tuning.
    /// </summary>
    private static void CorrectDayMacros(
        List<MealPlanItem> dayItems,
        Dictionary<int, Food> foodById,
        double targetProtein, double targetCarbs, double targetFat)
    {
        const double tolerance = 15.0; // Aim tighter than 20g to account for rounding
        const int groupPasses = 3;

        for (int pass = 0; pass < groupPasses; pass++)
        {
            var foodItems = dayItems.Where(i => i.MealName != "Protein Shake").ToList();

            var actualP = foodItems.Sum(i => i.ProteinG);
            var actualC = foodItems.Sum(i => i.CarbsG);
            var actualF = foodItems.Sum(i => i.FatG);

            var deltaP = actualP - targetProtein;
            var deltaC = actualC - targetCarbs;
            var deltaF = actualF - targetFat;

            if (Math.Abs(deltaP) <= tolerance && Math.Abs(deltaC) <= tolerance && Math.Abs(deltaF) <= tolerance)
                break;

            // Classify all food items by dominant macro
            var proteinItems = new List<(MealPlanItem item, Food food)>();
            var carbItems = new List<(MealPlanItem item, Food food)>();
            var fatItems = new List<(MealPlanItem item, Food food)>();

            foreach (var item in foodItems)
            {
                if (!foodById.TryGetValue(item.FoodId, out var food)) continue;
                if (food.CaloriesPer100g <= 0) continue;

                var pPct = food.ProteinPer100g * 4.0 / food.CaloriesPer100g;
                var fPct = food.FatPer100g * 9.0 / food.CaloriesPer100g;

                if (pPct > 0.30) proteinItems.Add((item, food));
                else if (fPct > 0.45) fatItems.Add((item, food));
                else carbItems.Add((item, food));
            }

            // Adjust protein-dominant items if protein is off
            if (Math.Abs(deltaP) > tolerance && proteinItems.Count > 0)
            {
                var groupP = proteinItems.Sum(x => x.item.ProteinG);
                if (groupP > 0)
                {
                    var scale = Math.Clamp((groupP - deltaP) / groupP, 0.4, 2.5);
                    AdjustGroup(proteinItems, scale);
                }
            }

            // Recalculate carb/fat deltas after protein adjustment
            actualC = dayItems.Where(i => i.MealName != "Protein Shake").Sum(i => i.CarbsG);
            actualF = dayItems.Where(i => i.MealName != "Protein Shake").Sum(i => i.FatG);
            deltaC = actualC - targetCarbs;
            deltaF = actualF - targetFat;

            // Adjust carb-dominant items if carbs are off
            if (Math.Abs(deltaC) > tolerance && carbItems.Count > 0)
            {
                var groupC = carbItems.Sum(x => x.item.CarbsG);
                if (groupC > 0)
                {
                    var scale = Math.Clamp((groupC - deltaC) / groupC, 0.4, 2.5);
                    AdjustGroup(carbItems, scale);
                }
            }

            // Recalculate fat delta
            actualF = dayItems.Where(i => i.MealName != "Protein Shake").Sum(i => i.FatG);
            deltaF = actualF - targetFat;

            // Adjust fat-dominant items if fat is off
            if (Math.Abs(deltaF) > tolerance && fatItems.Count > 0)
            {
                var groupF = fatItems.Sum(x => x.item.FatG);
                if (groupF > 0)
                {
                    var scale = Math.Clamp((groupF - deltaF) / groupF, 0.3, 3.0);
                    AdjustGroup(fatItems, scale);
                }
            }
        }

        // Fine-tune with single-item adjustments for any remaining deviation
        for (int iter = 0; iter < 10; iter++)
        {
            var foodItems = dayItems.Where(i => i.MealName != "Protein Shake").ToList();
            var actualP = foodItems.Sum(i => i.ProteinG);
            var actualC = foodItems.Sum(i => i.CarbsG);
            var actualF = foodItems.Sum(i => i.FatG);

            var deltaP = actualP - targetProtein;
            var deltaC = actualC - targetCarbs;
            var deltaF = actualF - targetFat;

            if (Math.Abs(deltaP) <= tolerance && Math.Abs(deltaC) <= tolerance && Math.Abs(deltaF) <= tolerance)
                break;

            // Find the worst macro
            var absDeltaP = Math.Abs(deltaP);
            var absDeltaC = Math.Abs(deltaC);
            var absDeltaF = Math.Abs(deltaF);

            string targetMacro;
            double targetDelta;
            if (absDeltaP >= absDeltaC && absDeltaP >= absDeltaF && absDeltaP > tolerance)
            { targetMacro = "protein"; targetDelta = deltaP; }
            else if (absDeltaC >= absDeltaP && absDeltaC >= absDeltaF && absDeltaC > tolerance)
            { targetMacro = "carbs"; targetDelta = deltaC; }
            else if (absDeltaF > tolerance)
            { targetMacro = "fat"; targetDelta = deltaF; }
            else break;

            // Find best item to adjust: highest concentration of the target macro
            MealPlanItem? bestItem = null;
            Food? bestFood = null;
            var bestRatio = -1.0;

            foreach (var item in foodItems)
            {
                if (!foodById.TryGetValue(item.FoodId, out var food)) continue;
                var totalMacroG = food.ProteinPer100g + food.CarbsPer100g + food.FatPer100g;
                if (totalMacroG <= 0) continue;

                var macroG = targetMacro switch
                {
                    "protein" => food.ProteinPer100g,
                    "carbs" => food.CarbsPer100g,
                    "fat" => food.FatPer100g,
                    _ => 0.0
                };
                var ratio = macroG / totalMacroG;

                if (targetDelta > 0 && item.ServingSizeG <= 15) continue; // Can't reduce further
                if (ratio > bestRatio)
                {
                    bestItem = item;
                    bestFood = food;
                    bestRatio = ratio;
                }
            }

            if (bestItem == null || bestFood == null) break;

            var macroPerGram = targetMacro switch
            {
                "protein" => bestFood.ProteinPer100g / 100.0,
                "carbs" => bestFood.CarbsPer100g / 100.0,
                "fat" => bestFood.FatPer100g / 100.0,
                _ => 0.0
            };

            if (macroPerGram < 0.01) break;

            // Calculate serving adjustment (negative delta → increase, positive → decrease)
            var servingChange = -targetDelta / macroPerGram;
            var maxChange = bestItem.ServingSizeG * 0.25;
            servingChange = Math.Clamp(servingChange, -maxChange, maxChange);

            var newServing = Math.Max(10, Math.Round(bestItem.ServingSizeG + servingChange));
            var factor = newServing / 100.0;
            bestItem.ServingSizeG = newServing;
            bestItem.Calories = Math.Round(bestFood.CaloriesPer100g * factor, 1);
            bestItem.ProteinG = Math.Round(bestFood.ProteinPer100g * factor, 1);
            bestItem.CarbsG = Math.Round(bestFood.CarbsPer100g * factor, 1);
            bestItem.FatG = Math.Round(bestFood.FatPer100g * factor, 1);
        }
    }

    private static void AdjustGroup(List<(MealPlanItem item, Food food)> group, double scale)
    {
        foreach (var (item, food) in group)
        {
            var newServing = Math.Max(10, Math.Round(item.ServingSizeG * scale));
            var factor = newServing / 100.0;
            item.ServingSizeG = newServing;
            item.Calories = Math.Round(food.CaloriesPer100g * factor, 1);
            item.ProteinG = Math.Round(food.ProteinPer100g * factor, 1);
            item.CarbsG = Math.Round(food.CarbsPer100g * factor, 1);
            item.FatG = Math.Round(food.FatPer100g * factor, 1);
        }
    }
}

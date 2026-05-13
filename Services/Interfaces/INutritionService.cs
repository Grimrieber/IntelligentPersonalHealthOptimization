using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface INutritionService
{
    double CalculateBMR(User user);
    double CalculateTDEE(double bmr, ActivityLevel activityLevel);
    (int proteinG, int carbsG, int fatG) CalculateMacroTargets(double tdee, FitnessGoal fitnessGoal);
    (int proteinG, int carbsG, int fatG) CalculateMacroTargets(double tdee, FitnessGoal fitnessGoal, DietType dietType);
    (int calories, int proteinG, int carbsG, int fatG) CalculateAssessmentTargets(
        double tdee, PrimaryNutritionGoal goal, DietType dietType, Gender gender,
        double currentWeightKg = 0, double targetWeightKg = 0, GoalTimeline timeline = GoalTimeline.TwelveWeeks,
        int workoutsPerWeek = 0, int avgWorkoutMinutes = 0,
        List<NutritionFocusArea>? focusAreas = null, int confidenceLevel = 5,
        double readinessScore = 5.0);
    Task<NutritionProfile> CreateNutritionProfileAsync(int userId, DietType dietType, int mealsPerDay,
        string allergies, string foodPreferences, string foodDislikes, int waterGlasses,
        string supplements, string alcoholFrequency, int caffeinePerDay,
        double bmr, double tdee, int targetCalories, int proteinG, int carbsG, int fatG);
    Task<NutritionProfile?> GetNutritionProfileAsync(int userId);
    Task<NutritionProfile> UpdateNutritionProfileAsync(NutritionProfile profile);
    Task<MealPlan> GenerateMealPlanAsync(int userId, int nutritionProfileId);
    Task<MealPlan?> GetActiveMealPlanAsync(int userId);
    Task<List<MealPlanDay>> GetMealPlanDaysAsync(int mealPlanId);
    Task<List<MealPlanItem>> GetMealItemsAsync(int mealPlanDayId);
    Task<MealPlan> RegenerateMealPlanAsync(int userId, int nutritionProfileId);
    Task<NutritionAssessment?> GetLatestAssessmentAsync(int userId);
    List<string> GetAvailableMealNames(MealType mealType, DietType dietType);
    List<MealTemplateInfo> GetAvailableMealsWithInfo(MealType mealType, DietType dietType, int targetCaloriesForSlot = 0);
    Task SwapMealAsync(int mealPlanDayId, MealType mealType, string mealTemplateName, int nutritionProfileId);
    Task<MealPlan> GenerateRecipeMealPlanAsync(int userId, int nutritionProfileId);
    Task<List<MealPlanItem>> GetTodaysMealPlanItemsAsync(int userId);
}

public class MealTemplateInfo
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Ingredients { get; set; } = string.Empty;
    public int EstimatedCalories { get; set; }
}

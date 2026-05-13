namespace IntelligentPersonalHealthOptimization.Models;

/// <summary>
/// Transient data holder used during the wellness check-in wizard flow.
/// Converted to WellnessCheckIn entity on completion.
/// </summary>
public class WellnessCheckInData
{
    // SCOFF
    public bool ScoffSick { get; set; }
    public bool ScoffControl { get; set; }
    public bool ScoffWeightLoss { get; set; }
    public bool ScoffBodyImage { get; set; }
    public bool ScoffFoodDominates { get; set; }

    // Eating Behaviors
    public bool BingeEating { get; set; }
    public bool RestrictToControl { get; set; }
    public bool GuiltAfterEating { get; set; }
    public bool EarnFoodExercise { get; set; }
    public bool SkipMeals { get; set; }
    public bool EatInSecret { get; set; }
    public bool RigidFoodRules { get; set; }
    public bool UseLaxativesDietPills { get; set; }

    // Mindset & Body Image
    public bool LabelFoodsGoodBad { get; set; }
    public bool MoodAffectedByFood { get; set; }
    public bool PreoccupiedWithFood { get; set; }
    public bool CompareBody { get; set; }
    public bool AvoidSocialFood { get; set; }
    public bool AnxiousWithoutTracking { get; set; }
    public bool WorthTiedToWeight { get; set; }

    // Physical Indicators
    public bool FrequentFatigue { get; set; }
    public bool RapidWeightChange { get; set; }
    public bool HormonalIssues { get; set; }
    public bool DentalThroatIssues { get; set; }
    public bool CompulsiveExercise { get; set; }
    public bool ColdOrHairLoss { get; set; }
    public bool Fainting { get; set; }

    // Nutrition Context
    public int MealsPerDay { get; set; } = 3;
    public int WeighFrequency { get; set; }
    public int DietCount { get; set; }
    public int PostMealFeeling { get; set; }
    public int FoodRelationshipFlags { get; set; }  // 0=Healthy, 1=Generally good, 2=Complicated, 3=Stressful, 4=Struggle
    public int FoodAvoidanceFlags { get; set; }     // 0=No, 1=Allergies/medical, 2=Religious/ethical, 3=Guilt, 4=Fear of weight gain

    // Computed
    public int ScoffTotal => (ScoffSick ? 1 : 0) + (ScoffControl ? 1 : 0) +
                             (ScoffWeightLoss ? 1 : 0) + (ScoffBodyImage ? 1 : 0) +
                             (ScoffFoodDominates ? 1 : 0);

    public int BehavioralScore =>
        (BingeEating ? 2 : 0) + (RestrictToControl ? 2 : 0) + (GuiltAfterEating ? 1 : 0) +
        (EarnFoodExercise ? 2 : 0) + (SkipMeals ? 1 : 0) + (EatInSecret ? 2 : 0) +
        (RigidFoodRules ? 1 : 0) + (UseLaxativesDietPills ? 3 : 0) +
        (LabelFoodsGoodBad ? 1 : 0) + (MoodAffectedByFood ? 1 : 0) +
        (PreoccupiedWithFood ? 2 : 0) + (CompareBody ? 1 : 0) +
        (AvoidSocialFood ? 2 : 0) + (AnxiousWithoutTracking ? 2 : 0) +
        (WorthTiedToWeight ? 2 : 0) +
        (FrequentFatigue ? 1 : 0) + (RapidWeightChange ? 2 : 0) +
        (HormonalIssues ? 2 : 0) + (DentalThroatIssues ? 2 : 0) +
        (CompulsiveExercise ? 2 : 0) + (ColdOrHairLoss ? 1 : 0) +
        (Fainting ? 2 : 0);

    /// <summary>
    /// Calculate risk level: 1=Low, 2=Moderate, 3=High, 4=Critical
    /// </summary>
    public int CalculateRiskLevel(int userAge)
    {
        // Age-adjusted SCOFF
        var adjustedScoff = ScoffTotal;
        if (userAge is >= 13 and <= 17)
            adjustedScoff += 1;
        else if (userAge is >= 18 and <= 25 && ScoffTotal > 0)
            adjustedScoff = (int)Math.Ceiling(ScoffTotal + 0.5);

        // SCOFF risk
        int scoffRisk;
        if (adjustedScoff >= 3) scoffRisk = 3;      // High
        else if (adjustedScoff >= 2) scoffRisk = 2;  // Moderate
        else scoffRisk = 1;                           // Low

        // Behavioral risk
        var behavScore = BehavioralScore;
        int behavRisk;
        if (behavScore >= 13) behavRisk = 4;         // Critical
        else if (behavScore >= 8) behavRisk = 3;     // High
        else if (behavScore >= 4) behavRisk = 2;     // Moderate
        else behavRisk = 1;                           // Low

        var finalRisk = Math.Max(scoffRisk, behavRisk);

        // Auto-escalate for dangerous behaviors
        if (UseLaxativesDietPills && finalRisk < 3) finalRisk = 3;
        if (Fainting && finalRisk < 3) finalRisk = 3;

        return finalRisk;
    }
}

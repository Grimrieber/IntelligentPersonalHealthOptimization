using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

public class WellnessCheckIn
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public DateTime CheckInDate { get; set; } = DateTime.UtcNow;

    // SCOFF Answers (1 = Yes, 0 = No)
    public bool ScoffSick { get; set; }         // S: Make yourself sick
    public bool ScoffControl { get; set; }      // C: Lost control over eating
    public bool ScoffWeightLoss { get; set; }   // O: Lost 14+ lbs in 3 months
    public bool ScoffBodyImage { get; set; }    // F: Believe fat when others say thin
    public bool ScoffFoodDominates { get; set; } // F: Food dominates life
    public int ScoffTotal { get; set; }         // Sum of yes answers (0-5)

    // Behavioral Checklist — Eating Behaviors
    public bool BingeEating { get; set; }           // B1: Eat large amounts, out of control
    public bool RestrictToControl { get; set; }     // B2: Restrict food to control weight
    public bool GuiltAfterEating { get; set; }      // B3: Feel guilty after eating
    public bool EarnFoodExercise { get; set; }      // B4: "Earn" food through exercise
    public bool SkipMeals { get; set; }             // B5: Skip meals intentionally
    public bool EatInSecret { get; set; }           // B6: Eat in secret or hide food
    public bool RigidFoodRules { get; set; }        // B7: Rigid/stressful food rules
    public bool UseLaxativesDietPills { get; set; } // B8: Laxatives, diet pills, etc.

    // Behavioral Checklist — Mindset & Body Image
    public bool LabelFoodsGoodBad { get; set; }     // M1: Label foods good/bad
    public bool MoodAffectedByFood { get; set; }    // M2: Mood affected by eating/weight
    public bool PreoccupiedWithFood { get; set; }   // M3: Frequent food/body thoughts
    public bool CompareBody { get; set; }           // M4: Compare body to others
    public bool AvoidSocialFood { get; set; }       // M5: Avoid social food situations
    public bool AnxiousWithoutTracking { get; set; } // M6: Anxious if can't track food
    public bool WorthTiedToWeight { get; set; }     // M7: Self-worth tied to weight

    // Behavioral Checklist — Physical Indicators
    public bool FrequentFatigue { get; set; }       // P1: Fatigue, dizziness
    public bool RapidWeightChange { get; set; }     // P2: Rapid weight loss/gain
    public bool HormonalIssues { get; set; }        // P3: Missed periods, low T, etc.
    public bool DentalThroatIssues { get; set; }    // P4: Dental problems, sore throat
    public bool CompulsiveExercise { get; set; }    // P5: Exercise when sick/injured
    public bool ColdOrHairLoss { get; set; }        // P6: Always cold, hair thinning
    public bool Fainting { get; set; }              // P7: Fainted or nearly fainted

    // Computed Scores
    public int BehavioralScore { get; set; }        // Weighted sum of behavioral items
    public int RiskLevel { get; set; }              // 1=Low, 2=Moderate, 3=High, 4=Critical

    // Nutrition Habits Context
    public int MealsPerDay { get; set; }            // 1-2, 3, 4+
    public int WeighFrequency { get; set; }         // 0=Never, 1=Weekly, 2=Daily, 3=Multiple/day
    public int DietCount { get; set; }              // Diets in last 2 years: 0=Never, 1=1-2, 2=3-5, 3=6+, 4=Always
    public int PostMealFeeling { get; set; }        // 0=Content, 1=Full but fine, 2=Guilty, 3=Compensate

    // Open-text keyword flags (not the text itself)
    public int FoodRelationshipFlags { get; set; }  // Count of concerning keywords detected
    public int FoodAvoidanceFlags { get; set; }     // 0=Medical/none, 1=Fear-based

    // Feature gating based on results
    public bool CalorieTrackingEnabled { get; set; } = true;
    public bool DeficitTargetEnabled { get; set; } = true;
    public bool WeightTrackingEnabled { get; set; } = true;
}

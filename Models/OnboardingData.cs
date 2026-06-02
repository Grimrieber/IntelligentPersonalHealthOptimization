using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Models;

public class OnboardingData
{
    // Step 2: Personal Info
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; } = DateTime.Today.AddYears(-25);
    public Gender Gender { get; set; } = Gender.Male;

    // Step 3: Body Metrics
    public double HeightCm { get; set; } = 170;
    public double WeightKg { get; set; } = 70;

    // Step 4: Health Screening
    public List<string> MedicalConditions { get; set; } = [];
    public List<string> InjuryAreas { get; set; } = [];

    // Step 5: Training Background
    public TrainingLocation TrainingLocation { get; set; } = TrainingLocation.Gym;
    public ExperienceLevel ExperienceLevel { get; set; } = ExperienceLevel.Beginner;
    public int CurrentFrequency { get; set; } = 3;
    public int SessionDurationMinutes { get; set; } = 60;
    public List<EquipmentType> AvailableEquipment { get; set; } = [EquipmentType.Bodyweight];
    public List<string> AvailableDays { get; set; } = ["Monday", "Wednesday", "Friday"];
    public string PreferredTimeOfDay { get; set; } = "Morning";

    // Step 6: Movement Assessment
    public List<MovementCompensation> OverheadSquatCompensations { get; set; } = [];
    public List<MovementCompensation> SingleLegLeftCompensations { get; set; } = [];
    public List<MovementCompensation> SingleLegRightCompensations { get; set; } = [];
    public List<PostureDeviation> PostureDeviations { get; set; } = [];
    public List<FlexibilityAssessmentData> FlexibilityResults { get; set; } = [];

    // Step 7: Fitness Benchmarks
    public int PushUpCount { get; set; }
    public int PlankHoldSeconds { get; set; }
    public int SquatCount { get; set; }
    public string CardioTestResult { get; set; } = string.Empty;

    // Step 8: Goals
    // INPUT-ONLY (goals consolidation Step 3): transient wizard input. Canonical storage is
    // User.FitnessGoal, written once in OnboardingCoordinator.CompleteOnboardingAsync. Do not
    // read this as a source of truth post-onboarding — read User.FitnessGoal instead.
    public FitnessGoal PrimaryFitnessGoal { get; set; } = FitnessGoal.GeneralFitness;
    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.ModeratelyActive;
    public List<GoalData> Goals { get; set; } = [];

    // Step 9: Nutrition
    public DietType DietType { get; set; } = DietType.Standard;
    public int MealsPerDay { get; set; } = 3;
    public List<FoodAllergy> Allergies { get; set; } = [];
    public int DailyWaterGlasses { get; set; } = 8;

    // Step 10: Security
    public string Pin { get; set; } = string.Empty;
    public string ConfirmPin { get; set; } = string.Empty;
    public string SecurityQuestion { get; set; } = string.Empty;
    public string SecurityAnswer { get; set; } = string.Empty;

    // Step 11: Disclaimer
    public bool HasAcceptedDisclaimer { get; set; }
}

public class FlexibilityAssessmentData
{
    public FlexibilityTest TestType { get; set; }
    public int RangeOfMotionDegrees { get; set; }
    public bool IsNormal { get; set; }
    public string Side { get; set; } = "Bilateral";
}

public class GoalData
{
    public GoalCategory GoalCategory { get; set; }
    public string Title { get; set; } = string.Empty;
    public double? TargetValue { get; set; }
    public string? TargetUnit { get; set; }
    public GoalTimeframe Timeframe { get; set; } = GoalTimeframe.MediumTerm;
}

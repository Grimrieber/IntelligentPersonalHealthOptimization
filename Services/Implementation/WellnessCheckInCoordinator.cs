using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class WellnessCheckInCoordinator : IWellnessCheckInCoordinator
{
    private readonly IDatabaseService _databaseService;
    private readonly IUserService _userService;

    private readonly string[] _stepTitles =
    [
        "Wellness Check-In",
        "How You Feel About Food",
        "Your Patterns & Habits",
        "Your Results"
    ];

    private readonly string[] _stepRoutes =
    [
        RouteConstants.WellnessIntro,
        RouteConstants.WellnessScoff,
        RouteConstants.WellnessBehavioral,
        RouteConstants.WellnessResult
    ];

    public WellnessCheckInCoordinator(IDatabaseService databaseService, IUserService userService)
    {
        _databaseService = databaseService;
        _userService = userService;
    }

    public int CurrentStep { get; private set; }
    public int TotalSteps => _stepTitles.Length;
    public string StepTitle => _stepTitles[CurrentStep];
    public double ProgressPercentage => (double)(CurrentStep + 1) / TotalSteps;
    public bool CanGoNext => CurrentStep < TotalSteps - 1;
    public bool CanGoPrevious => CurrentStep > 0;
    public WellnessCheckInData Data { get; private set; } = new();

    public async Task InitializeAsync()
    {
        CurrentStep = 0;
        Data = new WellnessCheckInData();

        // Pre-populate from previous check-in if one exists
        var user = await _userService.GetCurrentUserAsync();
        if (user == null) return;

        var previous = (await _databaseService.QueryAsync<WellnessCheckIn>(
            "SELECT * FROM WellnessCheckIn WHERE UserId = ? ORDER BY CheckInDate DESC LIMIT 1",
            user.Id)).FirstOrDefault();

        if (previous != null)
        {
            // Pre-populate nutrition context (these are more stable over time)
            Data.MealsPerDay = previous.MealsPerDay;
            Data.WeighFrequency = previous.WeighFrequency;
            Data.DietCount = previous.DietCount;
            // Don't pre-populate SCOFF or behavioral — user should answer fresh each time
        }
    }

    public async Task GoNextAsync()
    {
        if (!CanGoNext) return;
        CurrentStep++;
        await Shell.Current.GoToAsync(_stepRoutes[CurrentStep]);
    }

    public async Task GoPreviousAsync()
    {
        if (!CanGoPrevious) return;
        CurrentStep--;
        await Shell.Current.GoToAsync("..");
    }

    public async Task CompleteCheckInAsync()
    {
        var user = await _userService.GetCurrentUserAsync();
        if (user == null) return;

        var userAge = 30; // default
        try
        {
            if (user.DateOfBirth > DateTime.MinValue)
            {
                userAge = DateTime.UtcNow.Year - user.DateOfBirth.Year;
                if (user.DateOfBirth > DateTime.UtcNow.AddYears(-userAge)) userAge--;
            }
        }
        catch { /* fallback to default age */ }

        var riskLevel = Data.CalculateRiskLevel(userAge);

        // Food relationship: 0-1 = healthy, 2 = complicated, 3-4 = concerning
        var foodRelFlags = Data.FoodRelationshipFlags;

        // Food avoidance: 3-4 = fear/guilt-based (concerning), 0-2 = not concerning
        var avoidanceFlag = Data.FoodAvoidanceFlags >= 3 ? 1 : 0;

        // Determine feature gating
        var calorieTracking = riskLevel < 3;
        var deficitTarget = riskLevel < 2;
        var weightTracking = riskLevel < 3;

        var checkIn = new WellnessCheckIn
        {
            UserId = user.Id,
            CheckInDate = DateTime.UtcNow,

            // SCOFF
            ScoffSick = Data.ScoffSick,
            ScoffControl = Data.ScoffControl,
            ScoffWeightLoss = Data.ScoffWeightLoss,
            ScoffBodyImage = Data.ScoffBodyImage,
            ScoffFoodDominates = Data.ScoffFoodDominates,
            ScoffTotal = Data.ScoffTotal,

            // Eating Behaviors
            BingeEating = Data.BingeEating,
            RestrictToControl = Data.RestrictToControl,
            GuiltAfterEating = Data.GuiltAfterEating,
            EarnFoodExercise = Data.EarnFoodExercise,
            SkipMeals = Data.SkipMeals,
            EatInSecret = Data.EatInSecret,
            RigidFoodRules = Data.RigidFoodRules,
            UseLaxativesDietPills = Data.UseLaxativesDietPills,

            // Mindset
            LabelFoodsGoodBad = Data.LabelFoodsGoodBad,
            MoodAffectedByFood = Data.MoodAffectedByFood,
            PreoccupiedWithFood = Data.PreoccupiedWithFood,
            CompareBody = Data.CompareBody,
            AvoidSocialFood = Data.AvoidSocialFood,
            AnxiousWithoutTracking = Data.AnxiousWithoutTracking,
            WorthTiedToWeight = Data.WorthTiedToWeight,

            // Physical
            FrequentFatigue = Data.FrequentFatigue,
            RapidWeightChange = Data.RapidWeightChange,
            HormonalIssues = Data.HormonalIssues,
            DentalThroatIssues = Data.DentalThroatIssues,
            CompulsiveExercise = Data.CompulsiveExercise,
            ColdOrHairLoss = Data.ColdOrHairLoss,
            Fainting = Data.Fainting,

            // Scores
            BehavioralScore = Data.BehavioralScore,
            RiskLevel = riskLevel,

            // Context
            MealsPerDay = Data.MealsPerDay,
            WeighFrequency = Data.WeighFrequency,
            DietCount = Data.DietCount,
            PostMealFeeling = Data.PostMealFeeling,
            FoodRelationshipFlags = foodRelFlags,
            FoodAvoidanceFlags = avoidanceFlag,

            // Feature gating
            CalorieTrackingEnabled = calorieTracking,
            DeficitTargetEnabled = deficitTarget,
            WeightTrackingEnabled = weightTracking
        };

        await _databaseService.InsertAsync(checkIn);

        // Navigate back to nutrition tab
        await Shell.Current.GoToAsync("//Nutrition");
    }
}

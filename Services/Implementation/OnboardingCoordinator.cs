using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class OnboardingCoordinator : IOnboardingCoordinator
{
    private readonly IUserService _userService;
    private readonly ISecurityService _securityService;
    private readonly IAssessmentService _assessmentService;
    private readonly INutritionService _nutritionService;
    private readonly IGoalService _goalService;
    private readonly IScheduleService _scheduleService;
    private readonly INotificationService _notificationService;
    private readonly IPrescriptionEngine _prescriptionEngine;
    private readonly IDatabaseService _databaseService;

    private static readonly string[] StepTitles =
    [
        "Welcome",
        "Personal Info",
        "Body Metrics",
        "Health Screening",
        "Security",
        "Review & Finish"
    ];

    private static readonly string[] StepRoutes =
    [
        RouteConstants.OnboardingWelcome,
        RouteConstants.OnboardingPersonalInfo,
        RouteConstants.OnboardingBodyMetrics,
        RouteConstants.OnboardingHealthScreening,
        RouteConstants.OnboardingSecurity,
        RouteConstants.OnboardingReview
    ];

    public OnboardingCoordinator(
        IUserService userService,
        ISecurityService securityService,
        IAssessmentService assessmentService,
        INutritionService nutritionService,
        IGoalService goalService,
        IScheduleService scheduleService,
        INotificationService notificationService,
        IPrescriptionEngine prescriptionEngine,
        IDatabaseService databaseService)
    {
        _userService = userService;
        _securityService = securityService;
        _assessmentService = assessmentService;
        _nutritionService = nutritionService;
        _goalService = goalService;
        _scheduleService = scheduleService;
        _notificationService = notificationService;
        _prescriptionEngine = prescriptionEngine;
        _databaseService = databaseService;
    }

    public int CurrentStep { get; private set; }
    public int TotalSteps => StepTitles.Length;
    public string StepTitle => StepTitles[CurrentStep];
    public double ProgressPercentage => (CurrentStep + 1.0) / TotalSteps * 100;
    public bool CanGoNext => CurrentStep < TotalSteps - 1;
    public bool CanGoPrevious => CurrentStep > 0;
    public OnboardingData Data { get; private set; } = new();

    public async Task GoNextAsync()
    {
        if (!CanGoNext) return;
        CurrentStep++;
        await Shell.Current.GoToAsync(StepRoutes[CurrentStep]);
    }

    public async Task GoPreviousAsync()
    {
        if (!CanGoPrevious) return;
        CurrentStep--;
        await Shell.Current.GoToAsync("..");
    }

    public void Reset()
    {
        CurrentStep = 0;
        Data = new OnboardingData();
    }

    public async Task CompleteOnboardingAsync()
    {
        // 1. Create User record with basic info
        var user = new User
        {
            FirstName = Data.FirstName,
            LastName = Data.LastName,
            DateOfBirth = Data.DateOfBirth,
            Gender = Data.Gender,
            HeightCm = Data.HeightCm,
            WeightKg = Data.WeightKg,
            ActivityLevel = Data.ActivityLevel,
            FitnessGoal = Data.PrimaryFitnessGoal,
            HasAcceptedDisclaimer = Data.HasAcceptedDisclaimer
        };
        user = await _userService.CreateUserAsync(user);

        // 2. Set PIN + Security Question
        await _securityService.SetPinAsync(Data.Pin);
        if (!string.IsNullOrEmpty(Data.SecurityQuestion) && !string.IsNullOrEmpty(Data.SecurityAnswer))
            await _securityService.SetSecurityQuestionAsync(Data.SecurityQuestion, Data.SecurityAnswer);

        // 3. Create a basic Nutrition Profile with sensible defaults.
        // No nutrition assessment exists yet, so targets come from the FitnessGoal path
        // via the shared entry point (keeps onboarding/settings/assessment consistent).
        var t = _nutritionService.ComputeTargetsForUser(user, null);

        await _nutritionService.CreateNutritionProfileAsync(
            user.Id, DietType.Standard, 3,
            string.Empty, string.Empty, string.Empty,
            8, string.Empty, "None", 0,
            t.Bmr, t.Tdee, t.Calories, t.ProteinG, t.CarbsG, t.FatG);

        // 4. Create Goals
        foreach (var goalData in Data.Goals)
        {
            var goal = new UserGoal
            {
                UserId = user.Id,
                GoalCategory = goalData.GoalCategory,
                Title = goalData.Title,
                TargetValue = goalData.TargetValue,
                TargetUnit = goalData.TargetUnit,
                StartValue = goalData.GoalCategory == GoalCategory.WeightLoss ? Data.WeightKg : 0,
                Timeframe = goalData.Timeframe,
                DeadlineDate = goalData.Timeframe switch
                {
                    GoalTimeframe.ShortTerm => DateTime.UtcNow.AddDays(28),
                    GoalTimeframe.MediumTerm => DateTime.UtcNow.AddDays(84),
                    GoalTimeframe.LongTerm => DateTime.UtcNow.AddDays(180),
                    _ => DateTime.UtcNow.AddDays(84)
                }
            };
            await _goalService.CreateGoalAsync(goal);
        }

        // 5. Navigate to Dashboard
        // Assessments, training profile, workout programs, and meal plans
        // are created through the in-app assessment sections when the user is ready.
        await Shell.Current.GoToAsync($"//{RouteConstants.Dashboard}");
    }
}

using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class CesAssessmentCoordinator : ICesAssessmentCoordinator
{
    private readonly IDatabaseService _databaseService;
    private readonly IUserService _userService;

    private readonly string[] _stepTitles =
    [
        "CES Assessment",
        "Posture — Front View",
        "Posture — Side View",
        "Posture — Back View",
        "Overhead Squat",
        "Single-Leg Squat",
        "Push & Pull",
        "Your Results"
    ];

    private readonly string[] _stepRoutes =
    [
        RouteConstants.CesIntro,
        RouteConstants.CesPostureAnterior,
        RouteConstants.CesPostureLateral,
        RouteConstants.CesPosturePosterior,
        RouteConstants.CesOverheadSquat,
        RouteConstants.CesSingleLegSquat,
        RouteConstants.CesPushPull,
        RouteConstants.CesResult
    ];

    public CesAssessmentCoordinator(IDatabaseService databaseService, IUserService userService)
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
    public CesAssessment Data { get; private set; } = new();

    public async Task InitializeAsync()
    {
        CurrentStep = 0;
        Data = new CesAssessment();

        var user = await _userService.GetCurrentUserAsync();
        if (user == null) return;

        Data.UserId = user.Id;
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

    public async Task CompleteAssessmentAsync()
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            Data.UserId = user.Id;
            Data.AssessmentDate = DateTime.UtcNow;

            // Calculate scores
            Data.TotalCompensationScore = CesSyndromeEngine.CalculateTotalScore(Data);
            Data.OverallMovementScore = CesSyndromeEngine.CalculateMovementScore(Data.TotalCompensationScore);

            // Identify syndromes
            var syndromes = CesSyndromeEngine.IdentifySyndromes(Data);
            Data.IdentifiedSyndromes = string.Join(",", syndromes.Select(s => s.ToString()));

            // Calculate risk level
            var riskLevel = CesSyndromeEngine.CalculateRiskLevel(Data, syndromes);
            Data.RiskLevel = (int)riskLevel;

            // Save to database
            await _databaseService.InsertAsync(Data);

            await Shell.Current.DisplayAlert(
                "Assessment Saved",
                $"Your movement quality score is {Data.OverallMovementScore}/100. " +
                (syndromes.Count > 0
                    ? $"We identified {syndromes.Count} area(s) to address. Tap 'Generate Corrective Program' to create your personalized plan."
                    : "Your movement looks great! Keep up the good work."),
                "OK");

            // Return to the Workout tab — CES and workout-program-build are decoupled
            await Shell.Current.GoToAsync("//Workout");
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("CesAssessmentCoordinator.CompleteAsync", ex);
            throw;
        }
    }
}

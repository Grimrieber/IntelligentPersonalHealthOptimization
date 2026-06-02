using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Rules.MovementRules;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

[QueryProperty(nameof(SessionId), "sessionId")]
public partial class AssessmentResultViewModel : BaseViewModel
{
    private readonly IAssessmentService _assessmentService;
    private readonly IPrescriptionEngine _prescriptionEngine;
    private readonly IUserService _userService;

    public AssessmentResultViewModel(IAssessmentService assessmentService,
        IPrescriptionEngine prescriptionEngine, IUserService userService)
    {
        _assessmentService = assessmentService;
        _prescriptionEngine = prescriptionEngine;
        _userService = userService;
        Title = "Assessment Results";
    }

    [ObservableProperty]
    private int _sessionId;

    [ObservableProperty]
    private int _overallScore;

    [ObservableProperty]
    private string _scoreDescription = string.Empty;

    [ObservableProperty]
    private List<CompensationExplanation> _compensationExplanations = new();

    [ObservableProperty]
    private List<TestScoreItem> _testScores = new();

    [ObservableProperty]
    private bool _programGenerated;

    [ObservableProperty]
    private string _generatedProgramName = string.Empty;

    [RelayCommand]
    public async Task LoadResultsAsync()
    {
        if (SessionId <= 0) return;

        IsBusy = true;
        try
        {
            var results = await _assessmentService.GetResultsForSessionAsync(SessionId);
            OverallScore = _assessmentService.CalculateOverallScore(results);

            ScoreDescription = OverallScore switch
            {
                >= 80 => "Excellent movement quality! Your body is well-prepared for strength training.",
                >= 60 => "Good movement quality with some areas for improvement.",
                >= 40 => "Moderate movement quality. Corrective exercises will help address compensations.",
                _ => "Movement compensations detected. Your program will focus on corrective exercises first."
            };

            var testScoreList = new List<TestScoreItem>();
            var explanationList = new List<CompensationExplanation>();

            foreach (var result in results)
            {
                testScoreList.Add(new TestScoreItem
                {
                    TestName = FormatTestName(result.AssessmentType),
                    Score = result.Score,
                    MaxScore = 5
                });

                if (!string.IsNullOrEmpty(result.DetectedCompensations))
                {
                    var compensations = result.DetectedCompensations
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => Enum.Parse<MovementCompensation>(s.Trim()));

                    foreach (var comp in compensations)
                    {
                        explanationList.Add(new CompensationExplanation
                        {
                            CompensationName = FormatCompensationName(comp),
                            Explanation = OverheadSquatRules.GetCompensationExplanation(comp),
                            TestName = FormatTestName(result.AssessmentType)
                        });
                    }
                }
            }

            TestScores = testScoreList;
            CompensationExplanations = explanationList;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateProgramAsync()
    {
        // Route through the equipment intel flow before program generation.
        // Engine call lives on ProgramPreviewPage now.
        await Shell.Current.GoToAsync($"{RouteConstants.EquipmentIntro}?sessionId={SessionId}");
    }

    [RelayCommand]
    private async Task ViewProgramAsync()
    {
        await Shell.Current.GoToAsync($"//{RouteConstants.Workout}");
    }

    private static string FormatTestName(AssessmentType type) => type switch
    {
        AssessmentType.OverheadSquat => "Overhead Squat",
        AssessmentType.SingleLegBalanceLeft => "Single-Leg Balance (Left)",
        AssessmentType.SingleLegBalanceRight => "Single-Leg Balance (Right)",
        _ => type.ToString()
    };

    private static string FormatCompensationName(MovementCompensation comp) => comp switch
    {
        MovementCompensation.FeetTurnOut => "Feet Turn Out",
        MovementCompensation.FeetFlatten => "Feet Flatten",
        MovementCompensation.KneesValgus => "Knees Cave Inward",
        MovementCompensation.KneesDominant => "Knees Dominant",
        MovementCompensation.ExcessiveForwardLean => "Excessive Forward Lean",
        MovementCompensation.LowBackArches => "Low Back Arches",
        MovementCompensation.LowBackRounds => "Low Back Rounds",
        MovementCompensation.AsymmetricShift => "Asymmetric Weight Shift",
        MovementCompensation.ArmsForward => "Arms Fall Forward",
        MovementCompensation.ArmsUneven => "Arms Uneven",
        MovementCompensation.ShoulderElevation => "Shoulders Elevate",
        MovementCompensation.HipDrop => "Hip Drop",
        MovementCompensation.TrunkLateralLean => "Trunk Lateral Lean",
        MovementCompensation.AnklePronation => "Ankle Pronation",
        MovementCompensation.ExcessiveMovement => "Excessive Movement",
        MovementCompensation.LimitedDepth => "Limited Depth",
        MovementCompensation.PainReported => "Pain Reported",
        _ => comp.ToString()
    };
}

public class CompensationExplanation
{
    public string CompensationName { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
}

public class TestScoreItem
{
    public string TestName { get; set; } = string.Empty;
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string ScoreDisplay => $"{Score}/{MaxScore}";
}

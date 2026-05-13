using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class AssessmentViewModel : BaseViewModel
{
    private readonly IAssessmentService _assessmentService;
    private readonly IUserService _userService;

    public AssessmentViewModel(IAssessmentService assessmentService, IUserService userService)
    {
        _assessmentService = assessmentService;
        _userService = userService;
        Title = "Assessment";
        InitializeCompensationItems();
    }

    private int _currentSessionId;

    // Overhead Squat compensations
    [ObservableProperty]
    private List<CompensationItem> _overheadSquatCompensations = new();

    // Single Leg Balance compensations
    [ObservableProperty]
    private List<CompensationItem> _singleLegLeftCompensations = new();

    [ObservableProperty]
    private List<CompensationItem> _singleLegRightCompensations = new();

    private void InitializeCompensationItems()
    {
        OverheadSquatCompensations = new List<CompensationItem>
        {
            new(MovementCompensation.FeetTurnOut, "Feet Turn Out", "Feet/Ankle"),
            new(MovementCompensation.FeetFlatten, "Feet Flatten (Pronation)", "Feet/Ankle"),
            new(MovementCompensation.KneesValgus, "Knees Cave Inward", "Knees"),
            new(MovementCompensation.KneesDominant, "Heels Rise / Knees Dominant", "Knees"),
            new(MovementCompensation.ExcessiveForwardLean, "Excessive Forward Lean", "Hips/Trunk"),
            new(MovementCompensation.LowBackArches, "Low Back Arches", "Hips/Trunk"),
            new(MovementCompensation.LowBackRounds, "Low Back Rounds", "Hips/Trunk"),
            new(MovementCompensation.AsymmetricShift, "Asymmetric Weight Shift", "Hips/Trunk"),
            new(MovementCompensation.ArmsForward, "Arms Fall Forward", "Upper Body"),
            new(MovementCompensation.ArmsUneven, "Arms Uneven", "Upper Body"),
            new(MovementCompensation.ShoulderElevation, "Shoulders Elevate", "Upper Body"),
            new(MovementCompensation.LimitedDepth, "Limited Squat Depth", "General"),
            new(MovementCompensation.PainReported, "Pain During Movement", "General")
        };

        var singleLegComps = new Func<List<CompensationItem>>(() => new List<CompensationItem>
        {
            new(MovementCompensation.HipDrop, "Hip Drop", "Hip/Pelvis"),
            new(MovementCompensation.TrunkLateralLean, "Trunk Lateral Lean", "Trunk"),
            new(MovementCompensation.AnklePronation, "Ankle Pronation", "Ankle"),
            new(MovementCompensation.ExcessiveMovement, "Excessive Movement / Loss of Balance", "General")
        });

        SingleLegLeftCompensations = singleLegComps();
        SingleLegRightCompensations = singleLegComps();
    }

    [RelayCommand]
    private async Task StartSessionAsync()
    {
        var user = await _userService.GetCurrentUserAsync();
        if (user == null) return;

        // Reset compensation selections for a fresh assessment
        InitializeCompensationItems();

        var session = await _assessmentService.StartNewSessionAsync(user.Id);
        _currentSessionId = session.Id;

        await Shell.Current.GoToAsync(RouteConstants.OverheadSquat);
    }

    [RelayCommand]
    private async Task SaveOverheadSquatAndContinueAsync()
    {
        var selected = OverheadSquatCompensations
            .Where(c => c.IsSelected)
            .Select(c => c.Compensation)
            .ToList();

        int score = CalculateScore(selected.Count, OverheadSquatCompensations.Count);

        await _assessmentService.SaveResultAsync(
            _currentSessionId,
            AssessmentType.OverheadSquat,
            selected,
            score,
            string.Empty);

        await Shell.Current.GoToAsync(RouteConstants.SingleLegBalance);
    }

    [RelayCommand]
    private async Task CompleteAssessmentAsync()
    {
        // Save left side
        var leftSelected = SingleLegLeftCompensations
            .Where(c => c.IsSelected)
            .Select(c => c.Compensation)
            .ToList();
        int leftScore = CalculateScore(leftSelected.Count, SingleLegLeftCompensations.Count);
        await _assessmentService.SaveResultAsync(
            _currentSessionId,
            AssessmentType.SingleLegBalanceLeft,
            leftSelected,
            leftScore,
            string.Empty);

        // Save right side
        var rightSelected = SingleLegRightCompensations
            .Where(c => c.IsSelected)
            .Select(c => c.Compensation)
            .ToList();
        int rightScore = CalculateScore(rightSelected.Count, SingleLegRightCompensations.Count);
        await _assessmentService.SaveResultAsync(
            _currentSessionId,
            AssessmentType.SingleLegBalanceRight,
            rightSelected,
            rightScore,
            string.Empty);

        // Complete session
        await _assessmentService.CompleteSessionAsync(_currentSessionId);

        await Shell.Current.GoToAsync($"{RouteConstants.AssessmentResult}?sessionId={_currentSessionId}");
    }

    private static int CalculateScore(int compensationCount, int totalPossible)
    {
        if (totalPossible == 0) return 5;
        double ratio = (double)compensationCount / totalPossible;
        return ratio switch
        {
            0 => 5,
            <= 0.15 => 4,
            <= 0.3 => 3,
            <= 0.5 => 2,
            _ => 1
        };
    }
}

public partial class CompensationItem : ObservableObject
{
    public CompensationItem(MovementCompensation compensation, string displayName, string bodyRegion)
    {
        Compensation = compensation;
        DisplayName = displayName;
        BodyRegion = bodyRegion;
    }

    public MovementCompensation Compensation { get; }
    public string DisplayName { get; }
    public string BodyRegion { get; }

    [ObservableProperty]
    private bool _isSelected;
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class MovementAssessmentViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    public MovementAssessmentViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        // Initialize posture checkboxes
        PostureItems = Enum.GetValues<PostureDeviation>()
            .Select(p => new PostureCheckItem
            {
                Deviation = p,
                DisplayName = FormatDeviation(p),
                Description = GetDeviationDescription(p),
                IsChecked = _coordinator.Data.PostureDeviations.Contains(p)
            }).ToList();

        // Initialize overhead squat checkboxes
        OverheadSquatItems = GetOverheadSquatCompensations()
            .Select(c => new CompensationCheckItem
            {
                Compensation = c,
                DisplayName = FormatCompensation(c),
                IsChecked = _coordinator.Data.OverheadSquatCompensations.Contains(c)
            }).ToList();

        // Initialize single leg balance left
        SingleLegLeftItems = GetSingleLegCompensations()
            .Select(c => new CompensationCheckItem
            {
                Compensation = c,
                DisplayName = FormatCompensation(c),
                IsChecked = _coordinator.Data.SingleLegLeftCompensations.Contains(c)
            }).ToList();

        // Initialize single leg balance right
        SingleLegRightItems = GetSingleLegCompensations()
            .Select(c => new CompensationCheckItem
            {
                Compensation = c,
                DisplayName = FormatCompensation(c),
                IsChecked = _coordinator.Data.SingleLegRightCompensations.Contains(c)
            }).ToList();
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<PostureCheckItem> PostureItems { get; }
    public List<CompensationCheckItem> OverheadSquatItems { get; }
    public List<CompensationCheckItem> SingleLegLeftItems { get; }
    public List<CompensationCheckItem> SingleLegRightItems { get; }

    [RelayCommand]
    private void TogglePosture(PostureCheckItem item)
    {
        item.IsChecked = !item.IsChecked;
        SyncPosture();
    }

    [RelayCommand]
    private void ToggleOverheadSquat(CompensationCheckItem item)
    {
        item.IsChecked = !item.IsChecked;
        SyncOverheadSquat();
    }

    [RelayCommand]
    private void ToggleSingleLegLeft(CompensationCheckItem item)
    {
        item.IsChecked = !item.IsChecked;
        SyncSingleLegLeft();
    }

    [RelayCommand]
    private void ToggleSingleLegRight(CompensationCheckItem item)
    {
        item.IsChecked = !item.IsChecked;
        SyncSingleLegRight();
    }

    private void SyncPosture()
    {
        _coordinator.Data.PostureDeviations = PostureItems
            .Where(p => p.IsChecked)
            .Select(p => p.Deviation)
            .ToList();
    }

    private void SyncOverheadSquat()
    {
        _coordinator.Data.OverheadSquatCompensations = OverheadSquatItems
            .Where(c => c.IsChecked)
            .Select(c => c.Compensation)
            .ToList();
    }

    private void SyncSingleLegLeft()
    {
        _coordinator.Data.SingleLegLeftCompensations = SingleLegLeftItems
            .Where(c => c.IsChecked)
            .Select(c => c.Compensation)
            .ToList();
    }

    private void SyncSingleLegRight()
    {
        _coordinator.Data.SingleLegRightCompensations = SingleLegRightItems
            .Where(c => c.IsChecked)
            .Select(c => c.Compensation)
            .ToList();
    }

    private static List<MovementCompensation> GetOverheadSquatCompensations() =>
    [
        MovementCompensation.FeetTurnOut,
        MovementCompensation.FeetFlatten,
        MovementCompensation.KneesValgus,
        MovementCompensation.KneesDominant,
        MovementCompensation.ExcessiveForwardLean,
        MovementCompensation.LowBackArches,
        MovementCompensation.LowBackRounds,
        MovementCompensation.AsymmetricShift,
        MovementCompensation.ArmsForward,
        MovementCompensation.ArmsUneven,
        MovementCompensation.ShoulderElevation,
        MovementCompensation.LimitedDepth,
        MovementCompensation.PainReported
    ];

    private static List<MovementCompensation> GetSingleLegCompensations() =>
    [
        MovementCompensation.HipDrop,
        MovementCompensation.TrunkLateralLean,
        MovementCompensation.AnklePronation,
        MovementCompensation.ExcessiveMovement,
        MovementCompensation.LimitedDepth,
        MovementCompensation.PainReported
    ];

    private static string FormatCompensation(MovementCompensation c) => c switch
    {
        MovementCompensation.FeetTurnOut => "Feet Turn Out",
        MovementCompensation.FeetFlatten => "Feet Flatten (Pronation)",
        MovementCompensation.KneesValgus => "Knees Cave In (Valgus)",
        MovementCompensation.KneesDominant => "Knees Dominant (Shift Forward)",
        MovementCompensation.ExcessiveForwardLean => "Excessive Forward Lean",
        MovementCompensation.LowBackArches => "Low Back Arches",
        MovementCompensation.LowBackRounds => "Low Back Rounds",
        MovementCompensation.AsymmetricShift => "Asymmetric Weight Shift",
        MovementCompensation.ArmsForward => "Arms Fall Forward",
        MovementCompensation.ArmsUneven => "Arms Uneven",
        MovementCompensation.ShoulderElevation => "Shoulder Elevation",
        MovementCompensation.HipDrop => "Hip Drop",
        MovementCompensation.TrunkLateralLean => "Trunk Lateral Lean",
        MovementCompensation.AnklePronation => "Ankle Pronation",
        MovementCompensation.ExcessiveMovement => "Excessive Movement / Loss of Balance",
        MovementCompensation.LimitedDepth => "Limited Depth",
        MovementCompensation.PainReported => "Pain Reported",
        _ => c.ToString()
    };

    private static string FormatDeviation(PostureDeviation d) => d switch
    {
        PostureDeviation.ForwardHead => "Forward Head",
        PostureDeviation.RoundedShoulders => "Rounded Shoulders",
        PostureDeviation.Kyphosis => "Excessive Upper Back Rounding (Kyphosis)",
        PostureDeviation.Lordosis => "Excessive Low Back Curve (Lordosis)",
        PostureDeviation.FlatBack => "Flat Back",
        PostureDeviation.Swayback => "Swayback Posture",
        PostureDeviation.UnevenShoulders => "Uneven Shoulders",
        PostureDeviation.UnevenHips => "Uneven Hips",
        PostureDeviation.KneesHyperextended => "Knees Hyperextended",
        PostureDeviation.FeetPronated => "Feet Pronated (Flat Feet)",
        PostureDeviation.FeetSupinated => "Feet Supinated (High Arches)",
        PostureDeviation.AnteriorPelvicTilt => "Anterior Pelvic Tilt",
        PostureDeviation.PosteriorPelvicTilt => "Posterior Pelvic Tilt",
        _ => d.ToString()
    };

    private static string GetDeviationDescription(PostureDeviation d) => d switch
    {
        PostureDeviation.ForwardHead => "Head sits forward of shoulders when viewed from the side",
        PostureDeviation.RoundedShoulders => "Shoulders roll forward and inward",
        PostureDeviation.Kyphosis => "Upper back appears excessively rounded",
        PostureDeviation.Lordosis => "Lower back has an exaggerated inward curve",
        PostureDeviation.FlatBack => "Natural curve of lower back is reduced",
        PostureDeviation.Swayback => "Hips push forward past shoulders",
        PostureDeviation.UnevenShoulders => "One shoulder appears higher than the other",
        PostureDeviation.UnevenHips => "One hip appears higher or more forward",
        PostureDeviation.KneesHyperextended => "Knees lock back past straight",
        PostureDeviation.FeetPronated => "Arches collapse inward when standing",
        PostureDeviation.FeetSupinated => "Weight rolls to outside of feet",
        PostureDeviation.AnteriorPelvicTilt => "Pelvis tilts forward, belly pushes out",
        PostureDeviation.PosteriorPelvicTilt => "Pelvis tucks under, flattening lower back",
        _ => string.Empty
    };

    [RelayCommand]
    private async Task NextAsync()
    {
        SyncPosture();
        SyncOverheadSquat();
        SyncSingleLegLeft();
        SyncSingleLegRight();
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }
}

public partial class PostureCheckItem : ObservableObject
{
    public PostureDeviation Deviation { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isChecked;
}

public partial class CompensationCheckItem : ObservableObject
{
    public MovementCompensation Compensation { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isChecked;
}

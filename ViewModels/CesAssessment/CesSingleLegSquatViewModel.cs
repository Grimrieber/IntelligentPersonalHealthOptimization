using CommunityToolkit.Mvvm.ComponentModel;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

public partial class CesSingleLegSquatViewModel : CesStepViewModelBase
{
    public CesSingleLegSquatViewModel(ICesAssessmentCoordinator coordinator) : base(coordinator)
    {
        Title = "Single-Leg Squat";
    }

    // Left side
    [ObservableProperty] private int _leftKneeValgus;
    [ObservableProperty] private int _leftHipDrop;
    [ObservableProperty] private int _leftTrunkLean;
    [ObservableProperty] private int _leftFootPronation;

    // Right side
    [ObservableProperty] private int _rightKneeValgus;
    [ObservableProperty] private int _rightHipDrop;
    [ObservableProperty] private int _rightTrunkLean;
    [ObservableProperty] private int _rightFootPronation;

    public override Task LoadAsync()
    {
        LeftKneeValgus = Data.SlsLeftKneeValgus;
        LeftHipDrop = Data.SlsLeftHipDrop;
        LeftTrunkLean = Data.SlsLeftTrunkLean;
        LeftFootPronation = Data.SlsLeftFootPronation;
        RightKneeValgus = Data.SlsRightKneeValgus;
        RightHipDrop = Data.SlsRightHipDrop;
        RightTrunkLean = Data.SlsRightTrunkLean;
        RightFootPronation = Data.SlsRightFootPronation;
        return base.LoadAsync();
    }

    protected override void SyncToCoordinator()
    {
        Data.SlsLeftKneeValgus = LeftKneeValgus;
        Data.SlsLeftHipDrop = LeftHipDrop;
        Data.SlsLeftTrunkLean = LeftTrunkLean;
        Data.SlsLeftFootPronation = LeftFootPronation;
        Data.SlsRightKneeValgus = RightKneeValgus;
        Data.SlsRightHipDrop = RightHipDrop;
        Data.SlsRightTrunkLean = RightTrunkLean;
        Data.SlsRightFootPronation = RightFootPronation;
    }
}

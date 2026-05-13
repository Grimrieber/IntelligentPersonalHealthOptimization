using CommunityToolkit.Mvvm.ComponentModel;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

public partial class CesOverheadSquatViewModel : CesStepViewModelBase
{
    public CesOverheadSquatViewModel(ICesAssessmentCoordinator coordinator) : base(coordinator)
    {
        Title = "Overhead Squat";
    }

    [ObservableProperty] private int _feetTurnOut;
    [ObservableProperty] private int _feetFlatten;
    [ObservableProperty] private int _kneesValgus;
    [ObservableProperty] private int _forwardLean;
    [ObservableProperty] private int _lowBackArch;
    [ObservableProperty] private int _lowBackRound;
    [ObservableProperty] private int _heelsRise;
    [ObservableProperty] private int _armsFallForward;
    [ObservableProperty] private int _asymmetricShift;

    public override Task LoadAsync()
    {
        FeetTurnOut = Data.OhsFeetTurnOut;
        FeetFlatten = Data.OhsFeetFlatten;
        KneesValgus = Data.OhsKneesValgus;
        ForwardLean = Data.OhsForwardLean;
        LowBackArch = Data.OhsLowBackArch;
        LowBackRound = Data.OhsLowBackRound;
        HeelsRise = Data.OhsHeelsRise;
        ArmsFallForward = Data.OhsArmsFallForward;
        AsymmetricShift = Data.OhsAsymmetricShift;
        return base.LoadAsync();
    }

    protected override void SyncToCoordinator()
    {
        Data.OhsFeetTurnOut = FeetTurnOut;
        Data.OhsFeetFlatten = FeetFlatten;
        Data.OhsKneesValgus = KneesValgus;
        Data.OhsForwardLean = ForwardLean;
        Data.OhsLowBackArch = LowBackArch;
        Data.OhsLowBackRound = LowBackRound;
        Data.OhsHeelsRise = HeelsRise;
        Data.OhsArmsFallForward = ArmsFallForward;
        Data.OhsAsymmetricShift = AsymmetricShift;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

public partial class CesPostureAnteriorViewModel : CesStepViewModelBase
{
    public CesPostureAnteriorViewModel(ICesAssessmentCoordinator coordinator) : base(coordinator)
    {
        Title = "Posture — Front View";
    }

    [ObservableProperty] private int _feetTurnedOut;
    [ObservableProperty] private int _feetPronated;
    [ObservableProperty] private int _feetSupinated;
    [ObservableProperty] private int _kneesValgus;
    [ObservableProperty] private int _kneesVarus;
    [ObservableProperty] private int _unevenHips;
    [ObservableProperty] private int _unevenShoulders;
    [ObservableProperty] private int _headTilt;

    public override Task LoadAsync()
    {
        FeetTurnedOut = Data.FeetTurnedOut;
        FeetPronated = Data.FeetPronated;
        FeetSupinated = Data.FeetSupinated;
        KneesValgus = Data.KneesValgus;
        KneesVarus = Data.KneesVarus;
        UnevenHips = Data.UnevenHips;
        UnevenShoulders = Data.UnevenShoulders;
        HeadTilt = Data.HeadTilt;
        return base.LoadAsync();
    }

    protected override void SyncToCoordinator()
    {
        Data.FeetTurnedOut = FeetTurnedOut;
        Data.FeetPronated = FeetPronated;
        Data.FeetSupinated = FeetSupinated;
        Data.KneesValgus = KneesValgus;
        Data.KneesVarus = KneesVarus;
        Data.UnevenHips = UnevenHips;
        Data.UnevenShoulders = UnevenShoulders;
        Data.HeadTilt = HeadTilt;
    }
}

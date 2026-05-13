using CommunityToolkit.Mvvm.ComponentModel;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

public partial class CesPostureLateralViewModel : CesStepViewModelBase
{
    public CesPostureLateralViewModel(ICesAssessmentCoordinator coordinator) : base(coordinator)
    {
        Title = "Posture — Side View";
    }

    [ObservableProperty] private int _forwardHead;
    [ObservableProperty] private int _roundedShoulders;
    [ObservableProperty] private int _thoracicKyphosis;
    [ObservableProperty] private int _lumbarLordosis;
    [ObservableProperty] private int _flatBack;
    [ObservableProperty] private int _anteriorPelvicTilt;
    [ObservableProperty] private int _posteriorPelvicTilt;
    [ObservableProperty] private int _kneeHyperextension;

    public override Task LoadAsync()
    {
        ForwardHead = Data.ForwardHead;
        RoundedShoulders = Data.RoundedShoulders;
        ThoracicKyphosis = Data.ThoracicKyphosis;
        LumbarLordosis = Data.LumbarLordosis;
        FlatBack = Data.FlatBack;
        AnteriorPelvicTilt = Data.AnteriorPelvicTilt;
        PosteriorPelvicTilt = Data.PosteriorPelvicTilt;
        KneeHyperextension = Data.KneeHyperextension;
        return base.LoadAsync();
    }

    protected override void SyncToCoordinator()
    {
        Data.ForwardHead = ForwardHead;
        Data.RoundedShoulders = RoundedShoulders;
        Data.ThoracicKyphosis = ThoracicKyphosis;
        Data.LumbarLordosis = LumbarLordosis;
        Data.FlatBack = FlatBack;
        Data.AnteriorPelvicTilt = AnteriorPelvicTilt;
        Data.PosteriorPelvicTilt = PosteriorPelvicTilt;
        Data.KneeHyperextension = KneeHyperextension;
    }
}

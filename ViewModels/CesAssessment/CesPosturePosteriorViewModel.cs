using CommunityToolkit.Mvvm.ComponentModel;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

public partial class CesPosturePosteriorViewModel : CesStepViewModelBase
{
    public CesPosturePosteriorViewModel(ICesAssessmentCoordinator coordinator) : base(coordinator)
    {
        Title = "Posture — Back View";
    }

    [ObservableProperty] private int _scapularWinging;
    [ObservableProperty] private int _spinalDeviation;
    [ObservableProperty] private int _calcanealEversion;

    public override Task LoadAsync()
    {
        ScapularWinging = Data.ScapularWinging;
        SpinalDeviation = Data.SpinalDeviation;
        CalcanealEversion = Data.CalcanealEversion;
        return base.LoadAsync();
    }

    protected override void SyncToCoordinator()
    {
        Data.ScapularWinging = ScapularWinging;
        Data.SpinalDeviation = SpinalDeviation;
        Data.CalcanealEversion = CalcanealEversion;
    }
}

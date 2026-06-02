using CommunityToolkit.Mvvm.ComponentModel;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

public partial class CesPushPullViewModel : CesStepViewModelBase
{
    public CesPushPullViewModel(ICesAssessmentCoordinator coordinator) : base(coordinator)
    {
        Title = "Push & Pull Assessment";
    }

    // Push
    [ObservableProperty] private int _pushScapularWinging;
    [ObservableProperty] private int _pushShoulderHiking;
    [ObservableProperty] private int _pushForwardHeadPoke;
    [ObservableProperty] private int _pushLowBackSag;

    // Pull
    [ObservableProperty] private int _pullShoulderHiking;
    [ObservableProperty] private int _pullHeadProtrusion;
    [ObservableProperty] private int _pullLowBackExtension;

    // Pain reporting
    [ObservableProperty] private bool _painDuringAssessment;

    public override Task LoadAsync()
    {
        PushScapularWinging = Data.PushScapularWinging;
        PushShoulderHiking = Data.PushShoulderHiking;
        PushForwardHeadPoke = Data.PushForwardHeadPoke;
        PushLowBackSag = Data.PushLowBackSag;
        PullShoulderHiking = Data.PullShoulderHiking;
        PullHeadProtrusion = Data.PullHeadProtrusion;
        PullLowBackExtension = Data.PullLowBackExtension;
        PainDuringAssessment = Data.PainDuringAssessment;
        return base.LoadAsync();
    }

    protected override void SyncToCoordinator()
    {
        Data.PushScapularWinging = PushScapularWinging;
        Data.PushShoulderHiking = PushShoulderHiking;
        Data.PushForwardHeadPoke = PushForwardHeadPoke;
        Data.PushLowBackSag = PushLowBackSag;
        Data.PullShoulderHiking = PullShoulderHiking;
        Data.PullHeadProtrusion = PullHeadProtrusion;
        Data.PullLowBackExtension = PullLowBackExtension;
        Data.PainDuringAssessment = PainDuringAssessment;
    }
}

using IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

namespace IntelligentPersonalHealthOptimization.Views.CesAssessment;

public partial class CesPosturePosteriorPage : ContentPage
{
    private readonly CesPosturePosteriorViewModel _viewModel;

    public CesPosturePosteriorPage(CesPosturePosteriorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadAsync(); }
        catch (Exception ex) { Services.CrashLogger.Log("CesPosturePosteriorPage.OnAppearing", ex); }
    }
}

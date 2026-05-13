using IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

namespace IntelligentPersonalHealthOptimization.Views.CesAssessment;

public partial class CesPostureLateralPage : ContentPage
{
    private readonly CesPostureLateralViewModel _viewModel;

    public CesPostureLateralPage(CesPostureLateralViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadAsync(); }
        catch (Exception ex) { Services.CrashLogger.Log("CesPostureLateralPage.OnAppearing", ex); }
    }
}

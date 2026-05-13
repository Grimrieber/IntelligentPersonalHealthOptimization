using IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

namespace IntelligentPersonalHealthOptimization.Views.CesAssessment;

public partial class CesPostureAnteriorPage : ContentPage
{
    private readonly CesPostureAnteriorViewModel _viewModel;

    public CesPostureAnteriorPage(CesPostureAnteriorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadAsync(); }
        catch (Exception ex) { Services.CrashLogger.Log("CesPostureAnteriorPage.OnAppearing", ex); }
    }
}

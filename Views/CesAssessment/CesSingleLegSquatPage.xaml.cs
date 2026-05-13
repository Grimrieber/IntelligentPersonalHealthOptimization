using IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

namespace IntelligentPersonalHealthOptimization.Views.CesAssessment;

public partial class CesSingleLegSquatPage : ContentPage
{
    private readonly CesSingleLegSquatViewModel _viewModel;

    public CesSingleLegSquatPage(CesSingleLegSquatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadAsync(); }
        catch (Exception ex) { Services.CrashLogger.Log("CesSingleLegSquatPage.OnAppearing", ex); }
    }
}

using IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

namespace IntelligentPersonalHealthOptimization.Views.CesAssessment;

public partial class CesOverheadSquatPage : ContentPage
{
    private readonly CesOverheadSquatViewModel _viewModel;

    public CesOverheadSquatPage(CesOverheadSquatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadAsync(); }
        catch (Exception ex) { Services.CrashLogger.Log("CesOverheadSquatPage.OnAppearing", ex); }
    }
}

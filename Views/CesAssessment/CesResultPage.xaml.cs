using IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

namespace IntelligentPersonalHealthOptimization.Views.CesAssessment;

public partial class CesResultPage : ContentPage
{
    private readonly CesResultViewModel _viewModel;

    public CesResultPage(CesResultViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadResultsCommand.ExecuteAsync(null); }
        catch (Exception ex) { Services.CrashLogger.Log("CesResultPage.OnAppearing", ex); }
    }
}

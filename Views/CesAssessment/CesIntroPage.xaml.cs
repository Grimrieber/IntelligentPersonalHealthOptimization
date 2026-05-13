using IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

namespace IntelligentPersonalHealthOptimization.Views.CesAssessment;

public partial class CesIntroPage : ContentPage
{
    private readonly CesIntroViewModel _viewModel;

    public CesIntroPage(CesIntroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.InitializeCommand.ExecuteAsync(null); }
        catch (Exception ex) { Services.CrashLogger.Log("CesIntroPage.OnAppearing", ex); }
    }
}

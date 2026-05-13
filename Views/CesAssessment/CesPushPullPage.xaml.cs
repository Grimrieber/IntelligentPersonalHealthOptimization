using IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

namespace IntelligentPersonalHealthOptimization.Views.CesAssessment;

public partial class CesPushPullPage : ContentPage
{
    private readonly CesPushPullViewModel _viewModel;

    public CesPushPullPage(CesPushPullViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadAsync(); }
        catch (Exception ex) { Services.CrashLogger.Log("CesPushPullPage.OnAppearing", ex); }
    }
}

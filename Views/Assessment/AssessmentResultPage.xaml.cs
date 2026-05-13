using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Assessment;

public partial class AssessmentResultPage : ContentPage
{
    private readonly AssessmentResultViewModel _viewModel;

    public AssessmentResultPage(AssessmentResultViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.LoadResultsAsync();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("AssessmentResultPage OnAppearing", ex);
        }
    }
}

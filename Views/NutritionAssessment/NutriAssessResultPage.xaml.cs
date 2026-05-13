using IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

namespace IntelligentPersonalHealthOptimization.Views.NutritionAssessment;

public partial class NutriAssessResultPage : ContentPage
{
    private readonly NutriAssessResultViewModel _viewModel;

    public NutriAssessResultPage(NutriAssessResultViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.LoadResultsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("NutriAssessResultPage OnAppearing", ex);
        }
    }
}

using IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

namespace IntelligentPersonalHealthOptimization.Views.NutritionAssessment;

public partial class NutriAssessIntroPage : ContentPage
{
    private readonly NutriAssessIntroViewModel _viewModel;

    public NutriAssessIntroPage(NutriAssessIntroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.InitializeCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("NutriAssessIntroPage OnAppearing", ex);
        }
    }
}

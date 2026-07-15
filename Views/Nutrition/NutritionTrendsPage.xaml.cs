using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Nutrition;

public partial class NutritionTrendsPage : ContentPage
{
    private readonly NutritionTrendsViewModel _viewModel;

    public NutritionTrendsPage(NutritionTrendsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("NutritionTrendsPage OnAppearing", ex);
        }
    }
}

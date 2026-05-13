using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Nutrition;

public partial class NutritionDashboardPage : ContentPage
{
    private readonly NutritionDashboardViewModel _viewModel;

    public NutritionDashboardPage(NutritionDashboardViewModel viewModel)
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
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("NutritionDashboard OnAppearing", ex);
        }
    }
}

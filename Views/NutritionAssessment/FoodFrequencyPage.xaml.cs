using IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

namespace IntelligentPersonalHealthOptimization.Views.NutritionAssessment;

public partial class FoodFrequencyPage : ContentPage
{
    public FoodFrequencyPage(FoodFrequencyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (BindingContext is FoodFrequencyViewModel vm)
                await vm.InitializeCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("FoodFrequencyPage OnAppearing", ex);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as FoodFrequencyViewModel)?.SyncToCoordinator();
    }
}

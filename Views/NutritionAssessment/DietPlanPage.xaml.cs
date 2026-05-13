using IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

namespace IntelligentPersonalHealthOptimization.Views.NutritionAssessment;

public partial class DietPlanPage : ContentPage
{
    public DietPlanPage(DietPlanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as DietPlanViewModel)?.SyncToCoordinator();
    }
}

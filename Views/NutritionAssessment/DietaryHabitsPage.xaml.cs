using IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

namespace IntelligentPersonalHealthOptimization.Views.NutritionAssessment;

public partial class DietaryHabitsPage : ContentPage
{
    public DietaryHabitsPage(DietaryHabitsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        (BindingContext as DietaryHabitsViewModel)?.Initialize();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as DietaryHabitsViewModel)?.SyncToCoordinator();
    }
}

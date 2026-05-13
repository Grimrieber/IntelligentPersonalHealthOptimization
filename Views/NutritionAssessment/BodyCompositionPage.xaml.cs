using IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

namespace IntelligentPersonalHealthOptimization.Views.NutritionAssessment;

public partial class BodyCompositionPage : ContentPage
{
    public BodyCompositionPage(BodyCompositionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as BodyCompositionViewModel)?.SyncToCoordinator();
    }
}

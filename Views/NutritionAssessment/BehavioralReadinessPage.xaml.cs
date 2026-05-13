using IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

namespace IntelligentPersonalHealthOptimization.Views.NutritionAssessment;

public partial class BehavioralReadinessPage : ContentPage
{
    public BehavioralReadinessPage(BehavioralReadinessViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as BehavioralReadinessViewModel)?.SyncToCoordinator();
    }
}

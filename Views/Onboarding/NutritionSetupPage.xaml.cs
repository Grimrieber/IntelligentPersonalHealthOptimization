using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class NutritionSetupPage : ContentPage
{
    public NutritionSetupPage(NutritionSetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

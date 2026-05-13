using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class HealthScreeningPage : ContentPage
{
    public HealthScreeningPage(HealthScreeningViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

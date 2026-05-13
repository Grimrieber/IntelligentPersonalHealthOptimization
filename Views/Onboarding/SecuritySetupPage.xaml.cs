using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class SecuritySetupPage : ContentPage
{
    public SecuritySetupPage(SecuritySetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

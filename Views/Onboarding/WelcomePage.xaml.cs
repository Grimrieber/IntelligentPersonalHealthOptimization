using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class WelcomePage : ContentPage
{
    public WelcomePage(WelcomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

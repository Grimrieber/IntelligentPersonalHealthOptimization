using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class GoalsPage : ContentPage
{
    public GoalsPage(GoalsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

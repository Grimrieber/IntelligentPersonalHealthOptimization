using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class FitnessBenchmarkPage : ContentPage
{
    public FitnessBenchmarkPage(FitnessBenchmarkViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

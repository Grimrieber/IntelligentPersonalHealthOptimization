using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class TrainingBackgroundPage : ContentPage
{
    public TrainingBackgroundPage(TrainingBackgroundViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class MovementAssessmentPage : ContentPage
{
    public MovementAssessmentPage(MovementAssessmentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

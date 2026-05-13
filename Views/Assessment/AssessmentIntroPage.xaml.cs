using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Assessment;

public partial class AssessmentIntroPage : ContentPage
{
    public AssessmentIntroPage(AssessmentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

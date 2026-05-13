using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Assessment;

public partial class OverheadSquatPage : ContentPage
{
    public OverheadSquatPage(AssessmentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

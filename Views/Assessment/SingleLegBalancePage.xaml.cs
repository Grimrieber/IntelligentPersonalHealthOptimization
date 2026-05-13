using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Assessment;

public partial class SingleLegBalancePage : ContentPage
{
    public SingleLegBalancePage(AssessmentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

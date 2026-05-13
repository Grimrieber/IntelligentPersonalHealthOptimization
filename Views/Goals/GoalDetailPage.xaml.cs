using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Goals;

public partial class GoalDetailPage : ContentPage
{
    public GoalDetailPage(GoalDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

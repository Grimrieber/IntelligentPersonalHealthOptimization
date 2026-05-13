using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Goals;

public partial class AddGoalPage : ContentPage
{
    public AddGoalPage(AddGoalViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

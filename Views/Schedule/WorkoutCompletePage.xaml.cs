using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Schedule;

public partial class WorkoutCompletePage : ContentPage
{
    public WorkoutCompletePage(WorkoutCompleteViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

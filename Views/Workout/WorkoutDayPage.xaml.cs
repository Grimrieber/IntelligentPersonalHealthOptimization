using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Workout;

public partial class WorkoutDayPage : ContentPage
{
    public WorkoutDayPage(WorkoutDayViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

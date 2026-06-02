using IntelligentPersonalHealthOptimization.ViewModels.Workout;

namespace IntelligentPersonalHealthOptimization.Views.Workout;

public partial class ExerciseVideoPage : ContentPage
{
    public ExerciseVideoPage(ExerciseVideoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Workout;

public partial class WorkoutProgramPage : ContentPage
{
    private readonly WorkoutProgramViewModel _viewModel;

    public WorkoutProgramPage(WorkoutProgramViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.LoadProgramCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WorkoutProgramPage OnAppearing", ex);
        }
    }
}

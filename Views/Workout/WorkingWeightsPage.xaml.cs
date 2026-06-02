using IntelligentPersonalHealthOptimization.ViewModels.Workout;

namespace IntelligentPersonalHealthOptimization.Views.Workout;

public partial class WorkingWeightsPage : ContentPage
{
    private readonly WorkingWeightsViewModel _viewModel;

    public WorkingWeightsPage(WorkingWeightsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadCommand.ExecuteAsync(null); }
        catch (Exception ex) { Services.CrashLogger.Log("WorkingWeightsPage.OnAppearing", ex); }
    }
}

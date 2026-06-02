using IntelligentPersonalHealthOptimization.ViewModels.Workout;

namespace IntelligentPersonalHealthOptimization.Views.Workout;

public partial class EnvironmentDetailPage : ContentPage
{
    private readonly EnvironmentDetailViewModel _viewModel;

    public EnvironmentDetailPage(EnvironmentDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadCommand.ExecuteAsync(null); }
        catch (Exception ex) { Services.CrashLogger.Log("EnvironmentDetailPage.OnAppearing", ex); }
    }
}

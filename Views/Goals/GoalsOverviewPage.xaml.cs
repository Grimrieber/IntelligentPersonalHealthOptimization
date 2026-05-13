using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Goals;

public partial class GoalsOverviewPage : ContentPage
{
    private readonly GoalsOverviewViewModel _viewModel;

    public GoalsOverviewPage(GoalsOverviewViewModel viewModel)
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
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("GoalsOverviewPage OnAppearing", ex);
        }
    }
}

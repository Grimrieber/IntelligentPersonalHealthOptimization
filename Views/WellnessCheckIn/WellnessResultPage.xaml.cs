using IntelligentPersonalHealthOptimization.ViewModels.WellnessCheckIn;

namespace IntelligentPersonalHealthOptimization.Views.WellnessCheckIn;

public partial class WellnessResultPage : ContentPage
{
    private readonly WellnessResultViewModel _viewModel;

    public WellnessResultPage(WellnessResultViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.LoadResultsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WellnessResultPage OnAppearing", ex);
        }
    }
}

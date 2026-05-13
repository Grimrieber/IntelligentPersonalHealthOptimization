using IntelligentPersonalHealthOptimization.ViewModels.WellnessCheckIn;

namespace IntelligentPersonalHealthOptimization.Views.WellnessCheckIn;

public partial class WellnessScoffPage : ContentPage
{
    private readonly WellnessScoffViewModel _viewModel;

    public WellnessScoffPage(WellnessScoffViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WellnessScoffPage OnAppearing", ex);
        }
    }
}

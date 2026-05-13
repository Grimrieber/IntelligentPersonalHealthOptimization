using IntelligentPersonalHealthOptimization.ViewModels.WellnessCheckIn;

namespace IntelligentPersonalHealthOptimization.Views.WellnessCheckIn;

public partial class WellnessBehavioralPage : ContentPage
{
    private readonly WellnessBehavioralViewModel _viewModel;

    public WellnessBehavioralPage(WellnessBehavioralViewModel viewModel)
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
            Services.CrashLogger.Log("WellnessBehavioralPage OnAppearing", ex);
        }
    }
}

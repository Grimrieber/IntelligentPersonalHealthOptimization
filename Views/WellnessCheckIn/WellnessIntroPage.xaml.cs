using IntelligentPersonalHealthOptimization.ViewModels.WellnessCheckIn;

namespace IntelligentPersonalHealthOptimization.Views.WellnessCheckIn;

public partial class WellnessIntroPage : ContentPage
{
    private readonly WellnessIntroViewModel _viewModel;

    public WellnessIntroPage(WellnessIntroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.InitializeCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WellnessIntroPage OnAppearing", ex);
        }
    }
}

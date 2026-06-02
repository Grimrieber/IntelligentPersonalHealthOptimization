using IntelligentPersonalHealthOptimization.ViewModels.Workout;

namespace IntelligentPersonalHealthOptimization.Views.Workout;

public partial class ProgramPreviewPage : ContentPage
{
    private readonly ProgramPreviewViewModel _viewModel;

    public ProgramPreviewPage(ProgramPreviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadCommand.ExecuteAsync(null); }
        catch (Exception ex) { Services.CrashLogger.Log("ProgramPreviewPage.OnAppearing", ex); }
    }
}

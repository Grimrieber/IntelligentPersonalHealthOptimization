using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Nutrition;

public partial class EditTargetsPage : ContentPage
{
    private readonly EditTargetsViewModel _viewModel;

    public EditTargetsPage(EditTargetsViewModel viewModel)
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
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EditTargetsPage OnAppearing", ex);
        }
    }
}

using IntelligentPersonalHealthOptimization.ViewModels.Workout;

namespace IntelligentPersonalHealthOptimization.Views.Workout;

public partial class EquipmentIntroPage : ContentPage
{
    private readonly EquipmentIntroViewModel _viewModel;

    public EquipmentIntroPage(EquipmentIntroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadCommand.ExecuteAsync(null); }
        catch (Exception ex) { Services.CrashLogger.Log("EquipmentIntroPage.OnAppearing", ex); }
    }
}

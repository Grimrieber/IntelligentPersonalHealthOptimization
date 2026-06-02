using IntelligentPersonalHealthOptimization.ViewModels.Workout;

namespace IntelligentPersonalHealthOptimization.Views.Workout;

public partial class EquipmentDetailPage : ContentPage
{
    private readonly EquipmentDetailViewModel _viewModel;

    public EquipmentDetailPage(EquipmentDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _viewModel.LoadCommand.ExecuteAsync(null); }
        catch (Exception ex) { Services.CrashLogger.Log("EquipmentDetailPage.OnAppearing", ex); }
    }
}

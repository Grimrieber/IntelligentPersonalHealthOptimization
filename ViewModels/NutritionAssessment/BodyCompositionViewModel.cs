using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class BodyCompositionViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;

    public BodyCompositionViewModel(INutritionAssessmentCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        _neckCmText = _coordinator.Data.NeckCm > 0 ? _coordinator.Data.NeckCm.ToString() : string.Empty;
        _chestCmText = _coordinator.Data.ChestCm > 0 ? _coordinator.Data.ChestCm.ToString() : string.Empty;
        _waistCmText = _coordinator.Data.WaistCm > 0 ? _coordinator.Data.WaistCm.ToString() : string.Empty;
        _hipsCmText = _coordinator.Data.HipsCm > 0 ? _coordinator.Data.HipsCm.ToString() : string.Empty;
        _thighCmText = _coordinator.Data.ThighCm > 0 ? _coordinator.Data.ThighCm.ToString() : string.Empty;
        _calfCmText = _coordinator.Data.CalfCm > 0 ? _coordinator.Data.CalfCm.ToString() : string.Empty;
        _bicepCmText = _coordinator.Data.BicepCm > 0 ? _coordinator.Data.BicepCm.ToString() : string.Empty;
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    [ObservableProperty] private string _neckCmText = string.Empty;
    [ObservableProperty] private string _chestCmText = string.Empty;
    [ObservableProperty] private string _waistCmText = string.Empty;
    [ObservableProperty] private string _hipsCmText = string.Empty;
    [ObservableProperty] private string _thighCmText = string.Empty;
    [ObservableProperty] private string _calfCmText = string.Empty;
    [ObservableProperty] private string _bicepCmText = string.Empty;

    public void SyncToCoordinator()
    {
        _coordinator.Data.NeckCm = ParseDouble(NeckCmText);
        _coordinator.Data.ChestCm = ParseDouble(ChestCmText);
        _coordinator.Data.WaistCm = ParseDouble(WaistCmText);
        _coordinator.Data.HipsCm = ParseDouble(HipsCmText);
        _coordinator.Data.ThighCm = ParseDouble(ThighCmText);
        _coordinator.Data.CalfCm = ParseDouble(CalfCmText);
        _coordinator.Data.BicepCm = ParseDouble(BicepCmText);
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        SyncToCoordinator();
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        SyncToCoordinator();
        await _coordinator.GoPreviousAsync();
    }

    private static double ParseDouble(string text) =>
        double.TryParse(text, out var val) ? val : 0;
}

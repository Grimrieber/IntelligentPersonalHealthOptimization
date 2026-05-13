using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class NutriAssessIntroViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;

    public NutriAssessIntroViewModel(INutritionAssessmentCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    [ObservableProperty]
    private bool _isReassessment;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await _coordinator.InitializeAsync();
            IsReassessment = _coordinator.Data.WaistCm > 0
                          || _coordinator.Data.SelectedDietType != Models.Enums.DietType.Standard
                          || _coordinator.Data.PrimaryGoal != Models.Enums.PrimaryNutritionGoal.ImproveOverallHealth;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync($"//{RouteConstants.Nutrition}");
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.WellnessCheckIn;

public partial class WellnessIntroViewModel : BaseViewModel
{
    private readonly IWellnessCheckInCoordinator _coordinator;

    public WellnessIntroViewModel(IWellnessCheckInCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = "Wellness Check-In";
    }

    [ObservableProperty]
    private string _stepIndicator = string.Empty;

    [ObservableProperty]
    private double _progressPercentage;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await _coordinator.InitializeAsync();
            StepIndicator = $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";
            ProgressPercentage = _coordinator.ProgressPercentage;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WellnessIntro", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        await _coordinator.GoNextAsync();
    }
}

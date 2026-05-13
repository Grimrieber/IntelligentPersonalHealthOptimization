using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

public partial class CesIntroViewModel : CesStepViewModelBase
{
    public CesIntroViewModel(ICesAssessmentCoordinator coordinator) : base(coordinator)
    {
        Title = "CES Assessment";
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await Coordinator.InitializeAsync();
            UpdateProgress();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("CesIntro.Initialize", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

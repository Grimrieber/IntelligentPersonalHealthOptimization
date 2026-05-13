using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IWellnessCheckInCoordinator
{
    int CurrentStep { get; }
    int TotalSteps { get; }
    string StepTitle { get; }
    double ProgressPercentage { get; }
    bool CanGoNext { get; }
    bool CanGoPrevious { get; }
    WellnessCheckInData Data { get; }

    Task GoNextAsync();
    Task GoPreviousAsync();
    Task CompleteCheckInAsync();
    Task InitializeAsync();
}

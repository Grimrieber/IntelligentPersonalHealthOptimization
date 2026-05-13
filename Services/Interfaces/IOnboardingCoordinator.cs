using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IOnboardingCoordinator
{
    int CurrentStep { get; }
    int TotalSteps { get; }
    string StepTitle { get; }
    double ProgressPercentage { get; }
    bool CanGoNext { get; }
    bool CanGoPrevious { get; }
    OnboardingData Data { get; }

    Task GoNextAsync();
    Task GoPreviousAsync();
    Task CompleteOnboardingAsync();
    void Reset();
}

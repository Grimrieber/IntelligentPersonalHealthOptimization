using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface ICesAssessmentCoordinator
{
    int CurrentStep { get; }
    int TotalSteps { get; }
    string StepTitle { get; }
    double ProgressPercentage { get; }
    bool CanGoNext { get; }
    bool CanGoPrevious { get; }
    CesAssessment Data { get; }

    Task GoNextAsync();
    Task GoPreviousAsync();
    Task CompleteAssessmentAsync();
    Task InitializeAsync();
}

using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface INutritionAssessmentCoordinator
{
    int CurrentStep { get; }
    int TotalSteps { get; }
    string StepTitle { get; }
    double ProgressPercentage { get; }
    bool CanGoNext { get; }
    bool CanGoPrevious { get; }
    NutritionAssessmentData Data { get; }

    Task GoNextAsync();
    Task GoPreviousAsync();
    Task CompleteAssessmentAsync();
    Task InitializeAsync();
}

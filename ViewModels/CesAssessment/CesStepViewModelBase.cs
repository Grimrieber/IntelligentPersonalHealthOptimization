using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

/// <summary>
/// Base ViewModel for CES assessment steps. Provides common navigation,
/// progress tracking, and severity picker options.
/// </summary>
public abstract partial class CesStepViewModelBase : BaseViewModel
{
    protected readonly ICesAssessmentCoordinator Coordinator;

    protected CesStepViewModelBase(ICesAssessmentCoordinator coordinator)
    {
        Coordinator = coordinator;
    }

    [ObservableProperty] private string _stepIndicator = string.Empty;
    [ObservableProperty] private double _progressPercentage;

    /// <summary>Severity options for static posture observations.</summary>
    public string[] PostureSeverityOptions { get; } = ["Not Present", "Slight", "Noticeable", "Pronounced"];

    /// <summary>Severity options for dynamic movement assessments (reps-based).</summary>
    public string[] MovementSeverityOptions { get; } = ["Not Present", "Mild (1-2 of 5 reps)", "Moderate (3-4 of 5 reps)", "Severe (every rep)"];

    /// <summary>Access the shared assessment data.</summary>
    protected Models.CesAssessment Data => Coordinator.Data;

    /// <summary>Update progress indicators from coordinator state.</summary>
    protected void UpdateProgress()
    {
        StepIndicator = $"Step {Coordinator.CurrentStep + 1} of {Coordinator.TotalSteps}";
        ProgressPercentage = Coordinator.ProgressPercentage;
    }

    /// <summary>Override in subclass to load data from coordinator into UI properties.</summary>
    public virtual Task LoadAsync()
    {
        UpdateProgress();
        return Task.CompletedTask;
    }

    /// <summary>Override in subclass to sync UI properties back to coordinator data.</summary>
    protected virtual void SyncToCoordinator() { }

    [RelayCommand]
    protected async Task GoNextAsync()
    {
        SyncToCoordinator();
        await Coordinator.GoNextAsync();
    }

    [RelayCommand]
    protected async Task GoPreviousAsync()
    {
        SyncToCoordinator();
        await Coordinator.GoPreviousAsync();
    }
}

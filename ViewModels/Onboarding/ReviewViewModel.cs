using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class ReviewViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    public ReviewViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    // Personal Info Summary
    public string FullName => $"{_coordinator.Data.FirstName} {_coordinator.Data.LastName}";
    public string DateOfBirthDisplay => _coordinator.Data.DateOfBirth.ToString("MMMM dd, yyyy");
    public string GenderDisplay => _coordinator.Data.Gender.ToString();

    // Body Metrics Summary
    public string HeightDisplay
    {
        get
        {
            var totalInches = _coordinator.Data.HeightCm / 2.54;
            var feet = (int)(totalInches / 12);
            var inches = (int)(totalInches % 12);
            return $"{_coordinator.Data.HeightCm:F0} cm ({feet}'{inches}\")";
        }
    }

    public string WeightDisplay
    {
        get
        {
            var lbs = _coordinator.Data.WeightKg * 2.20462;
            return $"{_coordinator.Data.WeightKg:F1} kg ({lbs:F0} lbs)";
        }
    }

    public string BmiDisplay
    {
        get
        {
            var heightM = _coordinator.Data.HeightCm / 100.0;
            var bmi = _coordinator.Data.WeightKg / (heightM * heightM);
            return $"{bmi:F1}";
        }
    }

    // Goals Summary
    public string PrimaryGoalDisplay => _coordinator.Data.PrimaryFitnessGoal.ToString();
    public string ActivityLevelDisplay => _coordinator.Data.ActivityLevel.ToString();
    public string GoalsCountDisplay =>
        _coordinator.Data.Goals.Count > 0
            ? $"{_coordinator.Data.Goals.Count} goal(s) defined"
            : "No specific goals";
    public List<string> GoalsSummary =>
        _coordinator.Data.Goals.Select(g => $"{g.GoalCategory}: {g.Title}").ToList();

    // Disclaimer
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private bool _hasAcceptedDisclaimer;

    public bool CanCreate => HasAcceptedDisclaimer && !IsCreating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private bool _isCreating;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    partial void OnHasAcceptedDisclaimerChanged(bool value)
    {
        _coordinator.Data.HasAcceptedDisclaimer = value;
    }

    public string DisclaimerText { get; } =
        "MEDICAL DISCLAIMER: This application provides general fitness and nutrition " +
        "guidance based on the information you have provided. It is not a substitute for " +
        "professional medical advice, diagnosis, or treatment. Always consult with a " +
        "qualified healthcare provider before beginning any exercise or nutrition program, " +
        "especially if you have any medical conditions or injuries. By accepting this " +
        "disclaimer, you acknowledge that you use this application at your own risk and " +
        "that the developers are not liable for any injury, illness, or adverse effects " +
        "resulting from the use of this application or its recommendations.";

    [RelayCommand]
    private async Task CreatePlanAsync()
    {
        if (!HasAcceptedDisclaimer) return;

        IsCreating = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Setting up your account...";

        try
        {
            await _coordinator.CompleteOnboardingAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Something went wrong: {ex.Message}. Please try again.";
            StatusMessage = string.Empty;
            System.Diagnostics.Debug.WriteLine($"Onboarding completion error: {ex}");
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }
}

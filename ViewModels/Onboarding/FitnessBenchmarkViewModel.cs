using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class FitnessBenchmarkViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    public FitnessBenchmarkViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        _pushUpCount = _coordinator.Data.PushUpCount;
        _plankHoldSeconds = _coordinator.Data.PlankHoldSeconds;
        _squatCount = _coordinator.Data.SquatCount;
        _selectedCardioResult = _coordinator.Data.CardioTestResult;
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<string> CardioOptions { get; } =
        ["Can talk easily", "Can talk with effort", "Cannot talk", "Did not test"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverallFitnessLevel))]
    [NotifyPropertyChangedFor(nameof(PushUpRating))]
    private int _pushUpCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverallFitnessLevel))]
    [NotifyPropertyChangedFor(nameof(PlankRating))]
    private int _plankHoldSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverallFitnessLevel))]
    [NotifyPropertyChangedFor(nameof(SquatRating))]
    private int _squatCount;

    [ObservableProperty]
    private string _selectedCardioResult = string.Empty;

    public string PushUpRating => PushUpCount switch
    {
        >= 30 => "Excellent",
        >= 20 => "Good",
        >= 10 => "Average",
        >= 5 => "Below Average",
        _ => "Needs Work"
    };

    public string PlankRating => PlankHoldSeconds switch
    {
        >= 120 => "Excellent",
        >= 60 => "Good",
        >= 30 => "Average",
        >= 15 => "Below Average",
        _ => "Needs Work"
    };

    public string SquatRating => SquatCount switch
    {
        >= 30 => "Excellent",
        >= 20 => "Good",
        >= 10 => "Average",
        >= 5 => "Below Average",
        _ => "Needs Work"
    };

    public string OverallFitnessLevel
    {
        get
        {
            var score = 0;
            if (PushUpCount >= 30) score += 3;
            else if (PushUpCount >= 15) score += 2;
            else if (PushUpCount >= 5) score += 1;

            if (PlankHoldSeconds >= 120) score += 3;
            else if (PlankHoldSeconds >= 60) score += 2;
            else if (PlankHoldSeconds >= 30) score += 1;

            if (SquatCount >= 30) score += 3;
            else if (SquatCount >= 15) score += 2;
            else if (SquatCount >= 5) score += 1;

            return score switch
            {
                >= 7 => "Advanced",
                >= 4 => "Intermediate",
                _ => "Beginner"
            };
        }
    }

    partial void OnPushUpCountChanged(int value) => _coordinator.Data.PushUpCount = value;
    partial void OnPlankHoldSecondsChanged(int value) => _coordinator.Data.PlankHoldSeconds = value;
    partial void OnSquatCountChanged(int value) => _coordinator.Data.SquatCount = value;
    partial void OnSelectedCardioResultChanged(string value) => _coordinator.Data.CardioTestResult = value ?? string.Empty;

    [ObservableProperty]
    private string _pushUpText = string.Empty;

    [ObservableProperty]
    private string _plankText = string.Empty;

    [ObservableProperty]
    private string _squatText = string.Empty;

    partial void OnPushUpTextChanged(string value)
    {
        if (int.TryParse(value, out var count))
            PushUpCount = count;
    }

    partial void OnPlankTextChanged(string value)
    {
        if (int.TryParse(value, out var seconds))
            PlankHoldSeconds = seconds;
    }

    partial void OnSquatTextChanged(string value)
    {
        if (int.TryParse(value, out var count))
            SquatCount = count;
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class BodyMetricsViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    public BodyMetricsViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        _heightCm = _coordinator.Data.HeightCm;
        _weightKg = _coordinator.Data.WeightKg;
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeightDisplay))]
    [NotifyPropertyChangedFor(nameof(BmiDisplay))]
    private double _heightCm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeightDisplay))]
    [NotifyPropertyChangedFor(nameof(WeightLbDisplay))]
    [NotifyPropertyChangedFor(nameof(BmiDisplay))]
    private double _weightKg;

    public string HeightDisplay
    {
        get
        {
            var totalInches = HeightCm / 2.54;
            var feet = (int)(totalInches / 12);
            var inches = (int)(totalInches % 12);
            return $"{HeightCm:F0} cm / {feet}'{inches}\"";
        }
    }

    public string WeightDisplay
    {
        get
        {
            var lbs = WeightKg * 2.20462;
            return $"{WeightKg:F1} kg / {lbs:F0} lbs";
        }
    }

    public string WeightLbDisplay => $"{WeightKg * 2.20462:F0}";

    public string BmiDisplay
    {
        get
        {
            if (HeightCm <= 0) return string.Empty;
            var heightM = HeightCm / 100.0;
            var bmi = WeightKg / (heightM * heightM);
            var category = bmi switch
            {
                < 18.5 => "Underweight",
                < 25 => "Normal",
                < 30 => "Overweight",
                _ => "Obese"
            };
            return $"BMI: {bmi:F1} ({category})\n" +
                   "Note: BMI does not account for muscle mass, bone density, or body composition. " +
                   "Athletes and muscular individuals may show a high BMI despite having low body fat.";
        }
    }

    partial void OnHeightCmChanged(double value)
    {
        _coordinator.Data.HeightCm = value;
    }

    partial void OnWeightKgChanged(double value)
    {
        _coordinator.Data.WeightKg = value;
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

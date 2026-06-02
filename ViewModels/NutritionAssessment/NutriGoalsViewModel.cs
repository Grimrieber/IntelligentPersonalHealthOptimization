using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class NutriGoalsViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;
    private readonly IUserService _userService;
    private const int MaxFocusAreas = 5;
    private const double SafeWeeklyLossKg = 1.0;   // max safe weight loss per week
    private const double SafeWeeklyGainKg = 0.5;    // max safe weight gain per week
    private User? _currentUser;

    public NutriGoalsViewModel(INutritionAssessmentCoordinator coordinator, IUserService userService)
    {
        _coordinator = coordinator;
        _userService = userService;
        Title = _coordinator.StepTitle;

        _selectedPrimaryGoal = _coordinator.Data.PrimaryGoal;
        _selectedTimeline = _coordinator.Data.SelectedTimeline;
        _workoutsPerWeek = _coordinator.Data.WorkoutsPerWeek;
        _avgWorkoutMinutes = _coordinator.Data.AvgWorkoutMinutes > 0 ? _coordinator.Data.AvgWorkoutMinutes : 45;

        // Target weight is initialized in InitializeWeight() called from OnAppearing
        // to avoid MAUI Slider binding-order clamping issues

        FocusAreaItems = CreateFocusAreaItems();

        foreach (var item in FocusAreaItems)
        {
            item.IsChecked = _coordinator.Data.SelectedFocusAreas.Contains(item.Value);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CheckableItem<NutritionFocusArea>.IsChecked))
                    OnPropertyChanged(nameof(FocusAreaCountDisplay));
            };
        }
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<PrimaryNutritionGoal> PrimaryGoalOptions => Enum.GetValues<PrimaryNutritionGoal>().ToList();
    public List<GoalTimeline> TimelineOptions => Enum.GetValues<GoalTimeline>().ToList();

    [ObservableProperty]
    private PrimaryNutritionGoal _selectedPrimaryGoal;

    [ObservableProperty]
    private GoalTimeline _selectedTimeline;

    // Current weight + Target weight are stored in kg internally; both units displayed.
    // CurrentWeight is the user's actual weight today (persisted to User.WeightKg on Save).
    // TargetWeight is their goal weight (persisted to NutritionAssessmentData.TargetWeightKg).
    [ObservableProperty] private double _currentWeight;
    [ObservableProperty] private string _currentWeightKgDisplay = "—";
    [ObservableProperty] private string _currentWeightLbDisplay = "—";

    public List<ActivityLevel> ActivityLevelOptions => Enum.GetValues<ActivityLevel>().ToList();
    [ObservableProperty] private ActivityLevel _selectedActivityLevel;

    [ObservableProperty] private double _targetWeight;
    [ObservableProperty] private string _targetWeightKgDisplay = "—";
    [ObservableProperty] private string _targetWeightLbDisplay = "—";

    /// <summary>
    /// Must be called from OnAppearing after the Slider bindings are resolved.
    /// Setting Current/TargetWeight in the constructor causes MAUI's Slider to
    /// clamp the value before Min/Max bindings apply, losing the stored value.
    /// </summary>
    public void InitializeWeight()
    {
        if (_currentUser != null && _currentUser.WeightKg > 0)
            CurrentWeight = _currentUser.WeightKg;

        if (_currentUser != null)
            SelectedActivityLevel = _currentUser.ActivityLevel;

        var stored = _coordinator.Data.TargetWeightKg;
        if (stored > 0)
            TargetWeight = stored;
        else if (CurrentWeight > 0)
            TargetWeight = CurrentWeight;   // default target = current until user picks one

        UpdateWeightDisplay();
    }

    partial void OnCurrentWeightChanged(double value)
    {
        UpdateWeightDisplay();
        EvaluateSafetyWarning();
    }

    partial void OnTargetWeightChanged(double value)
    {
        UpdateWeightDisplay();
        EvaluateSafetyWarning();
    }

    partial void OnSelectedTimelineChanged(GoalTimeline value) => EvaluateSafetyWarning();
    partial void OnSelectedPrimaryGoalChanged(PrimaryNutritionGoal value) => EvaluateSafetyWarning();

    public async Task LoadCurrentWeightAsync()
    {
        _currentUser = await _userService.GetCurrentUserAsync();
    }

    // Safety warning properties
    [ObservableProperty] private bool _showSafetyWarning;
    [ObservableProperty] private string _safetyWarningText = string.Empty;
    [ObservableProperty] private string _suggestedTimelineText = string.Empty;

    private void EvaluateSafetyWarning()
    {
        if (CurrentWeight <= 0 || TargetWeight <= 0)
        {
            ShowSafetyWarning = false;
            return;
        }

        var targetKg = TargetWeight;
        var diffKg = targetKg - CurrentWeight;
        var absDiffKg = Math.Abs(diffKg);

        if (absDiffKg < 0.5)
        {
            ShowSafetyWarning = false;
            return;
        }

        var timelineWeeks = SelectedTimeline switch
        {
            GoalTimeline.SixWeeks => 6,
            GoalTimeline.EightWeeks => 8,
            GoalTimeline.TwelveWeeks => 12,
            GoalTimeline.SixMonths => 26,
            _ => 12
        };

        var weeklyChangeKg = absDiffKg / timelineWeeks;
        var isLoss = diffKg < 0;
        var safeWeeklyRate = isLoss ? SafeWeeklyLossKg : SafeWeeklyGainKg;

        if (weeklyChangeKg <= safeWeeklyRate)
        {
            ShowSafetyWarning = false;
            return;
        }

        // Calculate the minimum safe timeline
        var safeWeeksNeeded = (int)Math.Ceiling(absDiffKg / safeWeeklyRate);
        var direction = isLoss ? "lose" : "gain";
        var weeklyDisplay = isLoss ? "1 kg (2.2 lb)" : "0.5 kg (1.1 lb)";

        SafetyWarningText = $"Your goal to {direction} {absDiffKg:F1} kg in {timelineWeeks} weeks " +
                            $"requires {weeklyChangeKg:F1} kg/week, which exceeds the safe rate of {weeklyDisplay} per week. " +
                            $"Rapid weight change can lead to muscle loss, nutritional deficiencies, and metabolic slowdown.";

        // Suggest the right timeline option
        var suggestedTimeline = safeWeeksNeeded <= 6 ? "6 weeks"
            : safeWeeksNeeded <= 8 ? "8 weeks"
            : safeWeeksNeeded <= 12 ? "12 weeks"
            : safeWeeksNeeded <= 26 ? "6 months"
            : $"{safeWeeksNeeded} weeks";

        SuggestedTimelineText = $"For a safe and sustainable approach, choose at least {suggestedTimeline} " +
                                $"to reach your target weight.";
        ShowSafetyWarning = true;
    }

    private void UpdateWeightDisplay()
    {
        if (CurrentWeight > 0)
        {
            CurrentWeightKgDisplay = $"{CurrentWeight:F1}";
            CurrentWeightLbDisplay = $"{Math.Round(CurrentWeight * 2.20462, 1):F1}";
        }
        else
        {
            CurrentWeightKgDisplay = "—";
            CurrentWeightLbDisplay = "—";
        }

        if (TargetWeight > 0)
        {
            TargetWeightKgDisplay = $"{TargetWeight:F1}";
            TargetWeightLbDisplay = $"{Math.Round(TargetWeight * 2.20462, 1):F1}";
        }
        else
        {
            TargetWeightKgDisplay = "—";
            TargetWeightLbDisplay = "—";
        }
    }

    // Planned Exercise
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkoutSummary))]
    private int _workoutsPerWeek;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkoutSummary))]
    private int _avgWorkoutMinutes = 45;

    public string WorkoutSummary => WorkoutsPerWeek > 0
        ? $"{WorkoutsPerWeek}x/week, {AvgWorkoutMinutes} min each"
        : "No planned workouts";

    [RelayCommand]
    private void IncrementWorkouts()
    {
        if (WorkoutsPerWeek < 7) WorkoutsPerWeek++;
    }

    [RelayCommand]
    private void DecrementWorkouts()
    {
        if (WorkoutsPerWeek > 0) WorkoutsPerWeek--;
    }

    [RelayCommand]
    private void IncrementDuration()
    {
        if (AvgWorkoutMinutes < 120) AvgWorkoutMinutes += 15;
    }

    [RelayCommand]
    private void DecrementDuration()
    {
        if (AvgWorkoutMinutes > 15) AvgWorkoutMinutes -= 15;
    }

    public List<CheckableItem<NutritionFocusArea>> FocusAreaItems { get; }

    public string FocusAreaCountDisplay
    {
        get
        {
            var count = FocusAreaItems.Count(i => i.IsChecked);
            return $"{count}/{MaxFocusAreas} selected";
        }
    }

    public void SyncToCoordinator()
    {
        _coordinator.Data.PrimaryGoal = SelectedPrimaryGoal;
        _coordinator.Data.SelectedFocusAreas = FocusAreaItems
            .Where(i => i.IsChecked).Select(i => i.Value).ToList();
        _coordinator.Data.SelectedTimeline = SelectedTimeline;
        _coordinator.Data.TargetWeightKg = TargetWeight > 0 ? TargetWeight : 0;
        _coordinator.Data.WorkoutsPerWeek = WorkoutsPerWeek;
        _coordinator.Data.AvgWorkoutMinutes = AvgWorkoutMinutes;
    }

    /// <summary>
    /// Persist the user's current weight to the User table if it changed.
    /// Called when navigating away from the page.
    /// </summary>
    private async Task PersistCurrentWeightAsync()
    {
        if (_currentUser == null) return;

        var weightChanged = CurrentWeight > 0
                            && Math.Abs(_currentUser.WeightKg - CurrentWeight) >= 0.05;
        var activityChanged = _currentUser.ActivityLevel != SelectedActivityLevel;
        if (!weightChanged && !activityChanged) return;

        if (weightChanged) _currentUser.WeightKg = CurrentWeight;
        if (activityChanged) _currentUser.ActivityLevel = SelectedActivityLevel;
        await _userService.UpdateUserAsync(_currentUser);
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        SyncToCoordinator();
        await PersistCurrentWeightAsync();
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        SyncToCoordinator();
        await PersistCurrentWeightAsync();
        await _coordinator.GoPreviousAsync();
    }

    private static List<CheckableItem<NutritionFocusArea>> CreateFocusAreaItems() =>
    [
        new() { Value = NutritionFocusArea.IncreaseProtein, DisplayName = "Increase protein", Description = "Higher protein in your macro split" },
        new() { Value = NutritionFocusArea.IncludeHealthyFats, DisplayName = "Include healthy fats", Description = "Higher fat target (omega-3s, unsaturated)" },
        new() { Value = NutritionFocusArea.EatMoreFiber, DisplayName = "Eat more fiber", Description = "Shifts carbs toward fiber-rich foods" },
        new() { Value = NutritionFocusArea.EatMoreWholeGrains, DisplayName = "Eat more whole grains", Description = "Shifts carbs toward whole grains" },
        new() { Value = NutritionFocusArea.ReduceSugar, DisplayName = "Reduce sugar", Description = "Shifts macros away from simple carbs" },
        new() { Value = NutritionFocusArea.ReduceProcessedFood, DisplayName = "Reduce processed food", Description = "Favors whole foods over processed" }
    ];
}

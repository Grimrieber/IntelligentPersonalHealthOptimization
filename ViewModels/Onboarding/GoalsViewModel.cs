using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Helpers;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class GoalsViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    // ===== Curated goal types per category =====
    // Each type carries a default unit, suggested target value, and default timeframe.
    // Selecting a type auto-fills these into the entry to minimize typing.
    internal static readonly Dictionary<GoalCategory, List<GoalType>> GoalTypesByCategory = new()
    {
        [GoalCategory.WeightLoss] =
        [
            new("Lose weight", "kg", 5, GoalTimeframe.MediumTerm),
            new("Reduce body fat", "%", 5, GoalTimeframe.LongTerm),
            new("Drop a clothing size", "(none)", 0, GoalTimeframe.MediumTerm)
        ],
        [GoalCategory.MuscleGain] =
        [
            new("Gain lean mass", "kg", 3, GoalTimeframe.LongTerm),
            new("Increase arm size", "cm", 2, GoalTimeframe.MediumTerm),
            new("Increase chest size", "cm", 5, GoalTimeframe.LongTerm)
        ],
        [GoalCategory.BodyComposition] =
        [
            new("Reach target body fat %", "%", 15, GoalTimeframe.LongTerm),
            new("Maintain weight", "kg", 0, GoalTimeframe.LongTerm)
        ],
        [GoalCategory.Strength] =
        [
            new("Bench press", "kg", 80, GoalTimeframe.LongTerm),
            new("Back squat", "kg", 100, GoalTimeframe.LongTerm),
            new("Deadlift", "kg", 120, GoalTimeframe.LongTerm),
            new("Overhead press", "kg", 50, GoalTimeframe.LongTerm),
            new("Pull-ups", "reps", 10, GoalTimeframe.MediumTerm),
            new("Push-ups", "reps", 30, GoalTimeframe.ShortTerm),
            new("Dips", "reps", 10, GoalTimeframe.MediumTerm)
        ],
        [GoalCategory.Endurance] =
        [
            new("Run distance", "km", 5, GoalTimeframe.MediumTerm),
            new("Run time", "min", 30, GoalTimeframe.ShortTerm),
            new("Cycle distance", "km", 20, GoalTimeframe.MediumTerm),
            new("Plank hold", "sec", 60, GoalTimeframe.ShortTerm),
            new("Half marathon", "km", 21.1, GoalTimeframe.LongTerm),
            new("5K time", "min", 25, GoalTimeframe.MediumTerm)
        ],
        [GoalCategory.Flexibility] =
        [
            new("Touch toes comfortably", "(none)", 0, GoalTimeframe.MediumTerm),
            new("Full depth overhead squat", "(none)", 0, GoalTimeframe.MediumTerm),
            new("Bridge / backbend", "(none)", 0, GoalTimeframe.LongTerm),
            new("Overhead mobility", "(none)", 0, GoalTimeframe.MediumTerm)
        ],
        [GoalCategory.Habit] =
        [
            new("Workouts per week", "days/week", 3, GoalTimeframe.ShortTerm),
            new("Daily stretching", "days/week", 7, GoalTimeframe.ShortTerm),
            new("Daily walks", "days/week", 7, GoalTimeframe.ShortTerm),
            new("Hours of sleep", "hours/night", 8, GoalTimeframe.ShortTerm)
        ],
        [GoalCategory.GeneralHealth] =
        [
            new("Pain-free daily movement", "(none)", 0, GoalTimeframe.MediumTerm),
            new("Improve overall strength", "(none)", 0, GoalTimeframe.LongTerm),
            new("Improve overall fitness", "(none)", 0, GoalTimeframe.LongTerm),
            new("Reduce stress", "(none)", 0, GoalTimeframe.MediumTerm)
        ]
    };

    public static readonly List<string> AvailableUnits =
        ["kg", "lb", "%", "cm", "in", "km", "miles", "m", "min", "sec", "hours",
         "reps", "sets", "days/week", "hours/night", "(none)"];

    // ===== Top-level pickers (humanized) =====
    public List<PickerItem<FitnessGoal>> FitnessGoalOptions { get; } =
        PickerItem<FitnessGoal>.From(Enum.GetValues<FitnessGoal>());

    public List<PickerItem<ActivityLevel>> ActivityLevelOptions { get; } =
        PickerItem<ActivityLevel>.From(Enum.GetValues<ActivityLevel>());

    public List<PickerItem<GoalCategory>> GoalCategoryOptions { get; } =
        PickerItem<GoalCategory>.From(Enum.GetValues<GoalCategory>());

    public List<PickerItem<GoalTimeframe>> GoalTimeframeOptions { get; } =
        PickerItem<GoalTimeframe>.From(Enum.GetValues<GoalTimeframe>());

    public GoalsViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        SelectedPrimaryGoal = FitnessGoalOptions.FirstOrDefault(o => o.Value == _coordinator.Data.PrimaryFitnessGoal) ?? FitnessGoalOptions[0];
        SelectedActivityLevel = ActivityLevelOptions.FirstOrDefault(o => o.Value == _coordinator.Data.ActivityLevel) ?? ActivityLevelOptions[0];

        // Load existing goals
        foreach (var goal in _coordinator.Data.Goals)
        {
            var entry = new GoalEntryItem(
                GoalCategoryOptions,
                GoalTimeframeOptions,
                AvailableUnits)
            {
                SelectedCategory = GoalCategoryOptions.FirstOrDefault(o => o.Value == goal.GoalCategory) ?? GoalCategoryOptions[0],
                LegacyTitle = goal.Title,
                TargetValueText = goal.TargetValue?.ToString() ?? string.Empty,
                SelectedUnit = string.IsNullOrWhiteSpace(goal.TargetUnit) ? "(none)" : goal.TargetUnit!,
                SelectedTimeframe = GoalTimeframeOptions.FirstOrDefault(o => o.Value == goal.Timeframe) ?? GoalTimeframeOptions[1]
            };
            entry.RebuildGoalTypes();
            // If the legacy title matches a curated type for this category, select it
            entry.SelectedGoalType = entry.AvailableGoalTypes.FirstOrDefault(g => g.Name == goal.Title);

            Goals.Add(entry);
        }

        CanAddGoal = Goals.Count < 3;
        UpdateTemplateSuggestions(SelectedPrimaryGoal.Value);
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    [ObservableProperty] private PickerItem<FitnessGoal> _selectedPrimaryGoal;
    [ObservableProperty] private PickerItem<ActivityLevel> _selectedActivityLevel;

    public ObservableCollection<GoalEntryItem> Goals { get; } = [];

    [ObservableProperty] private bool _canAddGoal = true;

    // ===== Goal template suggestions (chip row) =====
    private record GoalTemplate(string Title, GoalCategory Category, double TargetValue, string Unit, GoalTimeframe Timeframe);

    private static readonly Dictionary<FitnessGoal, List<GoalTemplate>> SuggestedTemplatesByGoal = new()
    {
        [FitnessGoal.WeightLoss] =
        [
            new("Lose weight", GoalCategory.WeightLoss, 5, "kg", GoalTimeframe.MediumTerm),
            new("Workouts per week", GoalCategory.Habit, 4, "days/week", GoalTimeframe.ShortTerm),
            new("Reduce body fat", GoalCategory.BodyComposition, 20, "%", GoalTimeframe.LongTerm)
        ],
        [FitnessGoal.MuscleBuilding] =
        [
            new("Gain lean mass", GoalCategory.MuscleGain, 3, "kg", GoalTimeframe.MediumTerm),
            new("Bench press", GoalCategory.Strength, 80, "kg", GoalTimeframe.LongTerm),
            new("Back squat", GoalCategory.Strength, 100, "kg", GoalTimeframe.LongTerm)
        ],
        [FitnessGoal.GeneralFitness] =
        [
            new("Workouts per week", GoalCategory.Habit, 3, "days/week", GoalTimeframe.ShortTerm),
            new("Run distance", GoalCategory.Endurance, 5, "km", GoalTimeframe.MediumTerm),
            new("Improve overall strength", GoalCategory.GeneralHealth, 0, "(none)", GoalTimeframe.LongTerm)
        ],
        [FitnessGoal.ImprovedMobility] =
        [
            new("Touch toes comfortably", GoalCategory.Flexibility, 0, "(none)", GoalTimeframe.MediumTerm),
            new("Full depth overhead squat", GoalCategory.Flexibility, 0, "(none)", GoalTimeframe.MediumTerm),
            new("Daily stretching", GoalCategory.Habit, 7, "days/week", GoalTimeframe.ShortTerm)
        ],
        [FitnessGoal.Endurance] =
        [
            new("Run distance", GoalCategory.Endurance, 10, "km", GoalTimeframe.MediumTerm),
            new("Run time", GoalCategory.Endurance, 30, "min", GoalTimeframe.ShortTerm),
            new("Half marathon", GoalCategory.Endurance, 21.1, "km", GoalTimeframe.LongTerm)
        ],
        [FitnessGoal.Rehabilitation] =
        [
            new("Pain-free daily movement", GoalCategory.GeneralHealth, 0, "(none)", GoalTimeframe.MediumTerm),
            new("Improve overall strength", GoalCategory.GeneralHealth, 0, "(none)", GoalTimeframe.MediumTerm),
            new("Daily stretching", GoalCategory.Habit, 5, "days/week", GoalTimeframe.ShortTerm)
        ]
    };

    public ObservableCollection<GoalTemplateSuggestion> SuggestedTemplates { get; } = [];

    [ObservableProperty] private bool _hasTemplateSuggestions;

    partial void OnSelectedPrimaryGoalChanged(PickerItem<FitnessGoal> value)
    {
        _coordinator.Data.PrimaryFitnessGoal = value.Value;
        UpdateTemplateSuggestions(value.Value);
    }

    partial void OnSelectedActivityLevelChanged(PickerItem<ActivityLevel> value) =>
        _coordinator.Data.ActivityLevel = value.Value;

    private void UpdateTemplateSuggestions(FitnessGoal goal)
    {
        SuggestedTemplates.Clear();

        if (SuggestedTemplatesByGoal.TryGetValue(goal, out var templates))
        {
            foreach (var t in templates)
            {
                SuggestedTemplates.Add(new GoalTemplateSuggestion
                {
                    Title = t.Title,
                    Category = t.Category,
                    TargetValue = t.TargetValue,
                    Unit = t.Unit,
                    Timeframe = t.Timeframe
                });
            }
        }

        HasTemplateSuggestions = SuggestedTemplates.Count > 0;
    }

    [RelayCommand]
    private void ApplyTemplate(GoalTemplateSuggestion template)
    {
        if (Goals.Count >= 3) return;
        if (Goals.Any(g => g.SelectedGoalType?.Name == template.Title)) return;

        var entry = new GoalEntryItem(GoalCategoryOptions, GoalTimeframeOptions, AvailableUnits)
        {
            SelectedCategory = GoalCategoryOptions.FirstOrDefault(o => o.Value == template.Category) ?? GoalCategoryOptions[0]
        };
        entry.RebuildGoalTypes();
        entry.SelectedGoalType = entry.AvailableGoalTypes.FirstOrDefault(g => g.Name == template.Title);
        entry.TargetValueText = template.TargetValue > 0 ? template.TargetValue.ToString() : string.Empty;
        entry.SelectedUnit = string.IsNullOrWhiteSpace(template.Unit) ? "(none)" : template.Unit;
        entry.SelectedTimeframe = GoalTimeframeOptions.FirstOrDefault(o => o.Value == template.Timeframe) ?? GoalTimeframeOptions[1];

        Goals.Add(entry);
        CanAddGoal = Goals.Count < 3;
        SyncGoals();
    }

    [RelayCommand]
    private void AddGoal()
    {
        if (Goals.Count >= 3) return;

        var entry = new GoalEntryItem(GoalCategoryOptions, GoalTimeframeOptions, AvailableUnits)
        {
            SelectedCategory = GoalCategoryOptions.First(c => c.Value == GoalCategory.GeneralHealth),
            SelectedTimeframe = GoalTimeframeOptions.First(t => t.Value == GoalTimeframe.MediumTerm),
            SelectedUnit = "(none)"
        };
        entry.RebuildGoalTypes();

        Goals.Add(entry);
        CanAddGoal = Goals.Count < 3;
        SyncGoals();
    }

    [RelayCommand]
    private void RemoveGoal(GoalEntryItem goal)
    {
        Goals.Remove(goal);
        CanAddGoal = Goals.Count < 3;
        SyncGoals();
    }

    private void SyncGoals()
    {
        _coordinator.Data.Goals = Goals.Select(g => new GoalData
        {
            GoalCategory = g.SelectedCategory?.Value ?? GoalCategory.GeneralHealth,
            Title = g.SelectedGoalType?.Name ?? g.LegacyTitle,
            TargetValue = double.TryParse(g.TargetValueText, out var val) ? val : null,
            TargetUnit = g.SelectedUnit == "(none)" || string.IsNullOrWhiteSpace(g.SelectedUnit) ? null : g.SelectedUnit,
            Timeframe = g.SelectedTimeframe?.Value ?? GoalTimeframe.MediumTerm
        }).ToList();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        SyncGoals();
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }
}

/// <summary>
/// Curated goal type within a category. Drives auto-fill of unit/value/timeframe when chosen.
/// ToString override makes MAUI Pickers render the human name.
/// </summary>
public class GoalType
{
    public string Name { get; }
    public string DefaultUnit { get; }
    public double DefaultValue { get; }
    public GoalTimeframe DefaultTimeframe { get; }

    public GoalType(string name, string defaultUnit, double defaultValue, GoalTimeframe defaultTimeframe)
    {
        Name = name;
        DefaultUnit = defaultUnit;
        DefaultValue = defaultValue;
        DefaultTimeframe = defaultTimeframe;
    }

    public override string ToString() => Name;
}

public partial class GoalEntryItem : ObservableObject
{
    public List<PickerItem<GoalCategory>> CategoryOptions { get; }
    public List<PickerItem<GoalTimeframe>> TimeframeOptions { get; }
    public List<string> UnitOptions { get; }

    /// <summary>Used only to display legacy free-text titles loaded from prior saves.</summary>
    public string LegacyTitle { get; set; } = string.Empty;

    public GoalEntryItem(
        List<PickerItem<GoalCategory>> categoryOptions,
        List<PickerItem<GoalTimeframe>> timeframeOptions,
        List<string> unitOptions)
    {
        CategoryOptions = categoryOptions;
        TimeframeOptions = timeframeOptions;
        UnitOptions = unitOptions;
    }

    [ObservableProperty] private PickerItem<GoalCategory>? _selectedCategory;
    [ObservableProperty] private List<GoalType> _availableGoalTypes = [];
    [ObservableProperty] private GoalType? _selectedGoalType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConversionHint))]
    private string _targetValueText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConversionHint))]
    private string _selectedUnit = "(none)";

    [ObservableProperty] private PickerItem<GoalTimeframe>? _selectedTimeframe;

    /// <summary>
    /// Live conversion hint shown under the target value entry, e.g. "= 176 lb" when 80 kg.
    /// </summary>
    public string ConversionHint
    {
        get
        {
            if (!double.TryParse(TargetValueText, out var val) || val <= 0) return string.Empty;
            return SelectedUnit switch
            {
                "kg" => $"≈ {val * 2.20462:F0} lb",
                "lb" => $"≈ {val / 2.20462:F1} kg",
                "km" => $"≈ {val * 0.621371:F1} miles",
                "miles" => $"≈ {val / 0.621371:F1} km",
                "cm" => $"≈ {val * 0.393701:F1} in",
                "in" => $"≈ {val * 2.54:F1} cm",
                _ => string.Empty
            };
        }
    }

    public void RebuildGoalTypes()
    {
        if (SelectedCategory != null &&
            GoalsViewModel.GoalTypesByCategory.TryGetValue(SelectedCategory.Value, out var list))
        {
            AvailableGoalTypes = list;
        }
        else
        {
            AvailableGoalTypes = [];
        }
    }

    partial void OnSelectedCategoryChanged(PickerItem<GoalCategory>? value)
    {
        RebuildGoalTypes();
        SelectedGoalType = null;
    }

    partial void OnSelectedGoalTypeChanged(GoalType? value)
    {
        if (value == null) return;
        // Auto-fill defaults; user can still override
        TargetValueText = value.DefaultValue > 0 ? value.DefaultValue.ToString() : string.Empty;
        SelectedUnit = value.DefaultUnit;
        SelectedTimeframe = TimeframeOptions.FirstOrDefault(t => t.Value == value.DefaultTimeframe) ?? TimeframeOptions[1];
    }
}

public class GoalTemplateSuggestion
{
    public string Title { get; init; } = string.Empty;
    public GoalCategory Category { get; init; }
    public double TargetValue { get; init; }
    public string Unit { get; init; } = string.Empty;
    public GoalTimeframe Timeframe { get; init; }

    public string DisplayText
    {
        get
        {
            if (string.IsNullOrEmpty(Unit) || Unit == "(none)" || TargetValue == 0)
                return Title;

            // Always show kg + lb together; US-first display
            if (Unit == "kg")
                return $"{Title} ({TargetValue} kg / {TargetValue * 2.20462:F0} lb)";
            if (Unit == "lb")
                return $"{Title} ({TargetValue} lb / {TargetValue / 2.20462:F1} kg)";
            if (Unit == "km")
                return $"{Title} ({TargetValue} km / {TargetValue * 0.621371:F1} mi)";
            if (Unit == "cm")
                return $"{Title} ({TargetValue} cm / {TargetValue * 0.393701:F1} in)";

            return $"{Title} ({TargetValue} {Unit})";
        }
    }
}

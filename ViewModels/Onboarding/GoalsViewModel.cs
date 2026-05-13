using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class GoalsViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    private record GoalTemplate(string Title, GoalCategory Category, double TargetValue, string Unit, GoalTimeframe Timeframe);

    private static readonly Dictionary<FitnessGoal, List<GoalTemplate>> GoalTemplates = new()
    {
        [FitnessGoal.WeightLoss] =
        [
            new("Lose 5 kg", GoalCategory.WeightLoss, 5, "kg", GoalTimeframe.MediumTerm),
            new("Exercise 4x per week", GoalCategory.Habit, 4, "days/week", GoalTimeframe.ShortTerm),
            new("Reach 20% body fat", GoalCategory.BodyComposition, 20, "%", GoalTimeframe.LongTerm)
        ],
        [FitnessGoal.MuscleBuilding] =
        [
            new("Gain 3 kg muscle", GoalCategory.MuscleGain, 3, "kg", GoalTimeframe.MediumTerm),
            new("Bench press 80 kg", GoalCategory.Strength, 80, "kg", GoalTimeframe.LongTerm),
            new("Squat 100 kg", GoalCategory.Strength, 100, "kg", GoalTimeframe.LongTerm)
        ],
        [FitnessGoal.GeneralFitness] =
        [
            new("Exercise 3x per week", GoalCategory.Habit, 3, "days/week", GoalTimeframe.ShortTerm),
            new("Run 5 km without stopping", GoalCategory.Endurance, 5, "km", GoalTimeframe.MediumTerm),
            new("Improve overall strength", GoalCategory.GeneralHealth, 0, "", GoalTimeframe.LongTerm)
        ],
        [FitnessGoal.ImprovedMobility] =
        [
            new("Touch toes comfortably", GoalCategory.Flexibility, 0, "", GoalTimeframe.MediumTerm),
            new("Full depth overhead squat", GoalCategory.Flexibility, 0, "", GoalTimeframe.MediumTerm),
            new("Daily stretching routine", GoalCategory.Habit, 7, "days/week", GoalTimeframe.ShortTerm)
        ],
        [FitnessGoal.Endurance] =
        [
            new("Run 10 km", GoalCategory.Endurance, 10, "km", GoalTimeframe.MediumTerm),
            new("30 min sustained cardio", GoalCategory.Endurance, 30, "min", GoalTimeframe.ShortTerm),
            new("Complete a half marathon", GoalCategory.Endurance, 21.1, "km", GoalTimeframe.LongTerm)
        ],
        [FitnessGoal.Rehabilitation] =
        [
            new("Pain-free daily movement", GoalCategory.GeneralHealth, 0, "", GoalTimeframe.MediumTerm),
            new("Restore full range of motion", GoalCategory.Flexibility, 0, "", GoalTimeframe.MediumTerm),
            new("Consistent rehab exercises", GoalCategory.Habit, 5, "days/week", GoalTimeframe.ShortTerm)
        ]
    };

    public GoalsViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        _selectedPrimaryGoal = _coordinator.Data.PrimaryFitnessGoal;
        _selectedActivityLevel = _coordinator.Data.ActivityLevel;

        // Load existing goals
        foreach (var goal in _coordinator.Data.Goals)
        {
            Goals.Add(new GoalEntryItem
            {
                SelectedCategory = goal.GoalCategory,
                Title = goal.Title,
                TargetValueText = goal.TargetValue?.ToString() ?? string.Empty,
                TargetUnit = goal.TargetUnit ?? string.Empty,
                SelectedTimeframe = goal.Timeframe
            });
        }

        CanAddGoal = Goals.Count < 3;
        UpdateTemplateSuggestions(_selectedPrimaryGoal);
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<FitnessGoal> FitnessGoalOptions => Enum.GetValues<FitnessGoal>().ToList();
    public List<ActivityLevel> ActivityLevelOptions => Enum.GetValues<ActivityLevel>().ToList();
    public List<GoalCategory> GoalCategoryOptions => Enum.GetValues<GoalCategory>().ToList();
    public List<GoalTimeframe> GoalTimeframeOptions => Enum.GetValues<GoalTimeframe>().ToList();

    [ObservableProperty]
    private FitnessGoal _selectedPrimaryGoal;

    [ObservableProperty]
    private ActivityLevel _selectedActivityLevel;

    public ObservableCollection<GoalEntryItem> Goals { get; } = [];

    [ObservableProperty]
    private bool _canAddGoal = true;

    // Goal template suggestions
    public ObservableCollection<GoalTemplateSuggestion> SuggestedTemplates { get; } = [];

    [ObservableProperty]
    private bool _hasTemplateSuggestions;

    partial void OnSelectedPrimaryGoalChanged(FitnessGoal value)
    {
        _coordinator.Data.PrimaryFitnessGoal = value;
        UpdateTemplateSuggestions(value);
    }

    partial void OnSelectedActivityLevelChanged(ActivityLevel value) => _coordinator.Data.ActivityLevel = value;

    private void UpdateTemplateSuggestions(FitnessGoal goal)
    {
        SuggestedTemplates.Clear();

        if (GoalTemplates.TryGetValue(goal, out var templates))
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
        if (Goals.Any(g => g.Title == template.Title)) return;

        Goals.Add(new GoalEntryItem
        {
            SelectedCategory = template.Category,
            Title = template.Title,
            TargetValueText = template.TargetValue > 0 ? template.TargetValue.ToString() : string.Empty,
            TargetUnit = template.Unit,
            SelectedTimeframe = template.Timeframe
        });

        CanAddGoal = Goals.Count < 3;
        SyncGoals();
    }

    [RelayCommand]
    private void AddGoal()
    {
        if (Goals.Count >= 3) return;

        Goals.Add(new GoalEntryItem
        {
            SelectedCategory = GoalCategory.GeneralHealth,
            SelectedTimeframe = GoalTimeframe.MediumTerm
        });

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
            GoalCategory = g.SelectedCategory,
            Title = g.Title,
            TargetValue = double.TryParse(g.TargetValueText, out var val) ? val : null,
            TargetUnit = string.IsNullOrWhiteSpace(g.TargetUnit) ? null : g.TargetUnit,
            Timeframe = g.SelectedTimeframe
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

public partial class GoalEntryItem : ObservableObject
{
    [ObservableProperty]
    private GoalCategory _selectedCategory;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _targetValueText = string.Empty;

    [ObservableProperty]
    private string _targetUnit = string.Empty;

    [ObservableProperty]
    private GoalTimeframe _selectedTimeframe;
}

public class GoalTemplateSuggestion
{
    public string Title { get; init; } = string.Empty;
    public GoalCategory Category { get; init; }
    public double TargetValue { get; init; }
    public string Unit { get; init; } = string.Empty;
    public GoalTimeframe Timeframe { get; init; }
    public string DisplayText => string.IsNullOrEmpty(Unit) || TargetValue == 0
        ? Title
        : $"{Title} ({TargetValue} {Unit})";
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class GoalsOverviewViewModel : BaseViewModel
{
    private readonly IGoalService _goalService;
    private readonly IUserService _userService;

    public GoalsOverviewViewModel(IGoalService goalService, IUserService userService)
    {
        _goalService = goalService;
        _userService = userService;
        Title = "Goals";
    }

    [ObservableProperty]
    private ObservableCollection<GoalDisplayItem> _activeGoals = new();

    [ObservableProperty]
    private bool _hasGoals;

    [ObservableProperty]
    private int _activeGoalCount;

    [ObservableProperty]
    private int _completedGoalCount;

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var goals = await _goalService.GetActiveGoalsAsync(user.Id);
            var allGoals = await _goalService.GetAllGoalsAsync(user.Id);

            ActiveGoalCount = goals.Count;
            CompletedGoalCount = allGoals.Count(g => g.Status == GoalStatus.Completed);
            HasGoals = goals.Count > 0;

            var displayItems = new ObservableCollection<GoalDisplayItem>();

            foreach (var goal in goals)
            {
                double progressPct = _goalService.GetGoalProgressPercentage(goal);

                displayItems.Add(new GoalDisplayItem
                {
                    GoalId = goal.Id,
                    Title = goal.Title,
                    CategoryName = FormatGoalCategory(goal.GoalCategory),
                    CategoryIcon = GetCategoryIcon(goal.GoalCategory),
                    ProgressPercentage = progressPct,
                    ProgressText = $"{progressPct:F0}%",
                    ProgressBarValue = progressPct / 100.0,
                    DeadlineText = goal.DeadlineDate.HasValue
                        ? $"Due: {goal.DeadlineDate.Value:MMM dd, yyyy}"
                        : "No deadline",
                    CurrentValueText = goal.CurrentValue.HasValue && goal.TargetUnit != null
                        ? $"{goal.CurrentValue:F1} {goal.TargetUnit}"
                        : string.Empty,
                    TargetValueText = goal.TargetValue.HasValue && goal.TargetUnit != null
                        ? $"Target: {goal.TargetValue:F1} {goal.TargetUnit}"
                        : string.Empty,
                    TimeframeName = FormatTimeframe(goal.Timeframe),
                    StatusColor = GetStatusColor(goal.Status)
                });
            }

            ActiveGoals = displayItems;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Goals load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoToGoalDetailAsync(GoalDisplayItem item)
    {
        if (item == null) return;
        await Shell.Current.GoToAsync($"{RouteConstants.GoalDetail}?goalId={item.GoalId}");
    }

    [RelayCommand]
    private async Task AddGoalAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.AddGoal);
    }

    private static string FormatGoalCategory(GoalCategory category) => category switch
    {
        GoalCategory.WeightLoss => "Weight Loss",
        GoalCategory.MuscleGain => "Muscle Gain",
        GoalCategory.BodyComposition => "Body Composition",
        GoalCategory.Strength => "Strength",
        GoalCategory.Endurance => "Endurance",
        GoalCategory.Flexibility => "Flexibility",
        GoalCategory.Habit => "Habit",
        GoalCategory.GeneralHealth => "General Health",
        _ => category.ToString()
    };

    private static string GetCategoryIcon(GoalCategory category) => category switch
    {
        GoalCategory.WeightLoss => "scale.png",
        GoalCategory.MuscleGain => "muscle.png",
        GoalCategory.BodyComposition => "body.png",
        GoalCategory.Strength => "dumbbell.png",
        GoalCategory.Endurance => "running.png",
        GoalCategory.Flexibility => "stretch.png",
        GoalCategory.Habit => "check_circle.png",
        GoalCategory.GeneralHealth => "heart.png",
        _ => "target.png"
    };

    private static string FormatTimeframe(GoalTimeframe timeframe) => timeframe switch
    {
        GoalTimeframe.ShortTerm => "Short-term",
        GoalTimeframe.MediumTerm => "Medium-term",
        GoalTimeframe.LongTerm => "Long-term",
        _ => timeframe.ToString()
    };

    private static string GetStatusColor(GoalStatus status) => status switch
    {
        GoalStatus.Active => "#4CAF50",
        GoalStatus.Completed => "#2196F3",
        GoalStatus.Paused => "#FF9800",
        GoalStatus.Abandoned => "#9E9E9E",
        _ => "#9E9E9E"
    };
}

public class GoalDisplayItem
{
    public int GoalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryIcon { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
    public string ProgressText { get; set; } = string.Empty;
    public double ProgressBarValue { get; set; }
    public string DeadlineText { get; set; } = string.Empty;
    public string CurrentValueText { get; set; } = string.Empty;
    public string TargetValueText { get; set; } = string.Empty;
    public string TimeframeName { get; set; } = string.Empty;
    public string StatusColor { get; set; } = "#4CAF50";
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using Microcharts;
using SkiaSharp;

namespace IntelligentPersonalHealthOptimization.ViewModels;

[QueryProperty(nameof(GoalId), "goalId")]
public partial class GoalDetailViewModel : BaseViewModel
{
    private readonly IGoalService _goalService;

    public GoalDetailViewModel(IGoalService goalService)
    {
        _goalService = goalService;
        Title = "Goal Detail";
    }

    [ObservableProperty]
    private int _goalId;

    [ObservableProperty]
    private string _goalTitle = string.Empty;

    [ObservableProperty]
    private string _goalDescription = string.Empty;

    [ObservableProperty]
    private bool _hasDescription;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private string _statusName = string.Empty;

    [ObservableProperty]
    private string _statusColor = "#4CAF50";

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private double _progressBarValue;

    [ObservableProperty]
    private string _startValueText = string.Empty;

    [ObservableProperty]
    private string _currentValueText = string.Empty;

    [ObservableProperty]
    private string _targetValueText = string.Empty;

    [ObservableProperty]
    private string _timeframeName = string.Empty;

    [ObservableProperty]
    private string _deadlineText = string.Empty;

    [ObservableProperty]
    private string _createdDateText = string.Empty;

    [ObservableProperty]
    private bool _hasDeadline;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private ObservableCollection<MilestoneDisplayItem> _milestones = new();

    [ObservableProperty]
    private bool _hasMilestones;

    [ObservableProperty]
    private string _updateValueText = string.Empty;

    [ObservableProperty]
    private string _targetUnit = string.Empty;

    [ObservableProperty]
    private Chart? _progressChart;

    [ObservableProperty]
    private bool _hasChart;

    partial void OnGoalIdChanged(int value)
    {
        if (value > 0)
        {
            LoadDataCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var goal = await _goalService.GetGoalByIdAsync(GoalId);
            if (goal == null)
            {
                await Shell.Current.DisplayAlert("Error", "Goal not found.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            GoalTitle = goal.Title;
            GoalDescription = goal.Description ?? string.Empty;
            HasDescription = !string.IsNullOrEmpty(goal.Description);
            CategoryName = FormatGoalCategory(goal.GoalCategory);
            StatusName = FormatGoalStatus(goal.Status);
            StatusColor = GetStatusColor(goal.Status);
            IsActive = goal.Status == GoalStatus.Active;
            TargetUnit = goal.TargetUnit ?? string.Empty;

            double progressPct = _goalService.GetGoalProgressPercentage(goal);
            ProgressPercentage = progressPct;
            ProgressText = $"{progressPct:F0}%";
            ProgressBarValue = progressPct / 100.0;

            StartValueText = goal.StartValue.HasValue
                ? $"Start: {goal.StartValue:F1} {goal.TargetUnit}"
                : string.Empty;
            CurrentValueText = goal.CurrentValue.HasValue
                ? $"Current: {goal.CurrentValue:F1} {goal.TargetUnit}"
                : string.Empty;
            TargetValueText = goal.TargetValue.HasValue
                ? $"Target: {goal.TargetValue:F1} {goal.TargetUnit}"
                : string.Empty;

            TimeframeName = FormatTimeframe(goal.Timeframe);
            HasDeadline = goal.DeadlineDate.HasValue;
            DeadlineText = goal.DeadlineDate.HasValue
                ? goal.DeadlineDate.Value.ToString("MMMM dd, yyyy")
                : "No deadline set";
            CreatedDateText = goal.CreatedAt.ToLocalTime().ToString("MMMM dd, yyyy");

            var milestones = await _goalService.GetMilestonesAsync(GoalId);
            HasMilestones = milestones.Count > 0;
            Milestones = new ObservableCollection<MilestoneDisplayItem>(
                milestones.OrderBy(m => m.OrderIndex).Select(m => new MilestoneDisplayItem
                {
                    Id = m.Id,
                    Title = m.Title,
                    TargetValueText = $"{m.TargetValue:F1} {goal.TargetUnit}",
                    IsReached = m.IsReached,
                    ReachedDateText = m.ReachedDate.HasValue
                        ? m.ReachedDate.Value.ToLocalTime().ToString("MMM dd, yyyy")
                        : string.Empty,
                    StatusIcon = m.IsReached ? "check_circle_filled.png" : "circle_outline.png"
                }));

            BuildProgressChart(goal, milestones);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Goal detail load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildProgressChart(UserGoal goal, List<GoalMilestone> milestones)
    {
        if (!goal.StartValue.HasValue || !goal.TargetValue.HasValue || !goal.CurrentValue.HasValue)
        {
            HasChart = false;
            return;
        }

        var entries = new List<ChartEntry>();

        entries.Add(new ChartEntry((float)goal.StartValue.Value)
        {
            Label = "Start",
            ValueLabel = $"{goal.StartValue:F1}",
            Color = SKColor.Parse("#9E9E9E")
        });

        foreach (var milestone in milestones.Where(m => m.IsReached).OrderBy(m => m.OrderIndex))
        {
            entries.Add(new ChartEntry((float)milestone.TargetValue)
            {
                Label = milestone.Title,
                ValueLabel = $"{milestone.TargetValue:F1}",
                Color = SKColor.Parse("#4CAF50")
            });
        }

        entries.Add(new ChartEntry((float)goal.CurrentValue.Value)
        {
            Label = "Current",
            ValueLabel = $"{goal.CurrentValue:F1}",
            Color = SKColor.Parse("#512BD4")
        });

        entries.Add(new ChartEntry((float)goal.TargetValue.Value)
        {
            Label = "Target",
            ValueLabel = $"{goal.TargetValue:F1}",
            Color = SKColor.Parse("#2196F3")
        });

        ProgressChart = new LineChart
        {
            Entries = entries,
            LineMode = LineMode.Straight,
            LineSize = 3,
            PointMode = PointMode.Circle,
            PointSize = 12,
            BackgroundColor = SKColors.Transparent,
            LabelTextSize = 28
        };

        HasChart = entries.Count >= 2;
    }

    [RelayCommand]
    private async Task UpdateProgressAsync()
    {
        if (string.IsNullOrWhiteSpace(UpdateValueText)) return;
        if (!double.TryParse(UpdateValueText, out var newValue))
        {
            await Shell.Current.DisplayAlert("Invalid Input", "Please enter a valid number.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            await _goalService.UpdateGoalProgressAsync(GoalId, newValue);
            await _goalService.CheckAndUpdateMilestonesAsync(GoalId);
            UpdateValueText = string.Empty;
            await LoadDataAsync();

            await Shell.Current.DisplayAlert("Updated", "Your goal progress has been updated!", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to update progress: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CompleteGoalAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Complete Goal",
            "Mark this goal as completed?",
            "Complete", "Cancel");

        if (!confirmed) return;

        try
        {
            await _goalService.CompleteGoalAsync(GoalId);
            await Shell.Current.DisplayAlert("Congratulations!", "You've completed your goal!", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to complete goal: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task PauseGoalAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Pause Goal",
            "Pause this goal? You can resume it later.",
            "Pause", "Cancel");

        if (!confirmed) return;

        try
        {
            await _goalService.PauseGoalAsync(GoalId);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to pause goal: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task AbandonGoalAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Abandon Goal",
            "Are you sure you want to abandon this goal? This cannot be undone.",
            "Abandon", "Cancel");

        if (!confirmed) return;

        try
        {
            await _goalService.AbandonGoalAsync(GoalId);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to abandon goal: {ex.Message}", "OK");
        }
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

    private static string FormatGoalStatus(GoalStatus status) => status switch
    {
        GoalStatus.Active => "Active",
        GoalStatus.Completed => "Completed",
        GoalStatus.Paused => "Paused",
        GoalStatus.Abandoned => "Abandoned",
        _ => status.ToString()
    };

    private static string GetStatusColor(GoalStatus status) => status switch
    {
        GoalStatus.Active => "#4CAF50",
        GoalStatus.Completed => "#2196F3",
        GoalStatus.Paused => "#FF9800",
        GoalStatus.Abandoned => "#9E9E9E",
        _ => "#9E9E9E"
    };

    private static string FormatTimeframe(GoalTimeframe timeframe) => timeframe switch
    {
        GoalTimeframe.ShortTerm => "Short-term",
        GoalTimeframe.MediumTerm => "Medium-term",
        GoalTimeframe.LongTerm => "Long-term",
        _ => timeframe.ToString()
    };
}

public class MilestoneDisplayItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TargetValueText { get; set; } = string.Empty;
    public bool IsReached { get; set; }
    public string ReachedDateText { get; set; } = string.Empty;
    public string StatusIcon { get; set; } = "circle_outline.png";
}

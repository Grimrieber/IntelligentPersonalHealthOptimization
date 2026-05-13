using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class AddGoalViewModel : BaseViewModel
{
    private readonly IGoalService _goalService;
    private readonly IUserService _userService;

    public AddGoalViewModel(IGoalService goalService, IUserService userService)
    {
        _goalService = goalService;
        _userService = userService;
        Title = "Create Goal";
    }

    [ObservableProperty]
    private GoalCategory _selectedCategory = GoalCategory.GeneralHealth;

    [ObservableProperty]
    private string _goalTitle = string.Empty;

    [ObservableProperty]
    private string _goalDescription = string.Empty;

    [ObservableProperty]
    private string _targetValue = string.Empty;

    [ObservableProperty]
    private string _targetUnit = string.Empty;

    [ObservableProperty]
    private string _startValue = string.Empty;

    [ObservableProperty]
    private GoalTimeframe _selectedTimeframe = GoalTimeframe.MediumTerm;

    [ObservableProperty]
    private DateTime _deadlineDate = DateTime.Today.AddMonths(3);

    [ObservableProperty]
    private bool _hasDeadline = true;

    [ObservableProperty]
    private DateTime _minimumDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private ObservableCollection<MilestoneEntryItem> _milestoneEntries = new();

    [ObservableProperty]
    private bool _hasMilestones;

    [ObservableProperty]
    private string _titleError = string.Empty;

    [ObservableProperty]
    private bool _hasTitleError;

    [ObservableProperty]
    private string _targetError = string.Empty;

    [ObservableProperty]
    private bool _hasTargetError;

    public List<GoalCategory> GoalCategories { get; } = Enum.GetValues<GoalCategory>().ToList();

    public List<GoalTimeframe> GoalTimeframes { get; } = Enum.GetValues<GoalTimeframe>().ToList();

    partial void OnSelectedCategoryChanged(GoalCategory value)
    {
        TargetUnit = GetDefaultUnit(value);
    }

    [RelayCommand]
    private void AddMilestone()
    {
        if (MilestoneEntries.Count >= 3) return;

        MilestoneEntries.Add(new MilestoneEntryItem
        {
            OrderIndex = MilestoneEntries.Count + 1,
            Placeholder = $"Milestone {MilestoneEntries.Count + 1}"
        });
        HasMilestones = MilestoneEntries.Count > 0;
    }

    [RelayCommand]
    private void RemoveMilestone(MilestoneEntryItem milestone)
    {
        if (milestone == null) return;
        MilestoneEntries.Remove(milestone);

        for (int i = 0; i < MilestoneEntries.Count; i++)
        {
            MilestoneEntries[i].OrderIndex = i + 1;
            MilestoneEntries[i].Placeholder = $"Milestone {i + 1}";
        }
        HasMilestones = MilestoneEntries.Count > 0;
    }

    [RelayCommand]
    private async Task CreateGoalAsync()
    {
        if (!ValidateForm()) return;

        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            double.TryParse(TargetValue, out var target);
            double.TryParse(StartValue, out var start);

            var goal = new UserGoal
            {
                UserId = user.Id,
                GoalCategory = SelectedCategory,
                Title = GoalTitle.Trim(),
                Description = string.IsNullOrWhiteSpace(GoalDescription) ? null : GoalDescription.Trim(),
                TargetValue = target,
                TargetUnit = string.IsNullOrWhiteSpace(TargetUnit) ? null : TargetUnit.Trim(),
                StartValue = start,
                CurrentValue = start,
                Timeframe = SelectedTimeframe,
                DeadlineDate = HasDeadline ? DeadlineDate : null,
                Status = GoalStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            var createdGoal = await _goalService.CreateGoalAsync(goal);

            foreach (var milestone in MilestoneEntries)
            {
                if (!string.IsNullOrWhiteSpace(milestone.Title) && double.TryParse(milestone.TargetValueText, out var milestoneTarget))
                {
                    await _goalService.AddMilestoneAsync(createdGoal.Id, milestone.Title.Trim(), milestoneTarget, milestone.OrderIndex);
                }
            }

            await Shell.Current.DisplayAlert("Success", $"Goal '{goal.Title}' has been created!", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to create goal: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ValidateForm()
    {
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(GoalTitle))
        {
            TitleError = "Goal title is required";
            HasTitleError = true;
            isValid = false;
        }
        else
        {
            TitleError = string.Empty;
            HasTitleError = false;
        }

        if (string.IsNullOrWhiteSpace(TargetValue) || !double.TryParse(TargetValue, out _))
        {
            TargetError = "Please enter a valid target value";
            HasTargetError = true;
            isValid = false;
        }
        else
        {
            TargetError = string.Empty;
            HasTargetError = false;
        }

        return isValid;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private static string GetDefaultUnit(GoalCategory category) => category switch
    {
        GoalCategory.WeightLoss => "kg",
        GoalCategory.MuscleGain => "kg",
        GoalCategory.BodyComposition => "%",
        GoalCategory.Strength => "kg",
        GoalCategory.Endurance => "min",
        GoalCategory.Flexibility => "cm",
        GoalCategory.Habit => "days",
        GoalCategory.GeneralHealth => string.Empty,
        _ => string.Empty
    };
}

public class MilestoneEntryItem : ObservableObject
{
    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _targetValueText = string.Empty;
    public string TargetValueText
    {
        get => _targetValueText;
        set => SetProperty(ref _targetValueText, value);
    }

    private int _orderIndex;
    public int OrderIndex
    {
        get => _orderIndex;
        set => SetProperty(ref _orderIndex, value);
    }

    private string _placeholder = string.Empty;
    public string Placeholder
    {
        get => _placeholder;
        set => SetProperty(ref _placeholder, value);
    }
}

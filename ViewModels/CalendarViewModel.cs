using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class CalendarViewModel : BaseViewModel
{
    private readonly IScheduleService _scheduleService;
    private readonly IUserService _userService;
    private readonly IDatabaseService _databaseService;

    public CalendarViewModel(IScheduleService scheduleService, IUserService userService, IDatabaseService databaseService)
    {
        _scheduleService = scheduleService;
        _userService = userService;
        _databaseService = databaseService;
        Title = "Calendar";
        _displayMonth = DateTime.Today;
    }

    [ObservableProperty]
    private DateTime _displayMonth;

    [ObservableProperty]
    private string _monthYearText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CalendarDayItem> _calendarDays = [];

    [ObservableProperty]
    private CalendarDayItem? _selectedDay;

    [ObservableProperty]
    private string _selectedDayWorkoutName = string.Empty;

    [ObservableProperty]
    private string _selectedDayStatus = string.Empty;

    [ObservableProperty]
    private bool _hasSelectedDay;

    [ObservableProperty]
    private bool _canCompleteSelectedWorkout;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _missedCount;

    [ObservableProperty]
    private int _scheduledCount;

    [ObservableProperty]
    private int _currentStreak;

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            MonthYearText = DisplayMonth.ToString("MMMM yyyy");

            var firstOfMonth = new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
            var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

            var workouts = await _scheduleService.GetScheduledWorkoutsAsync(user.Id, firstOfMonth, lastOfMonth);

            var days = new List<CalendarDayItem>();
            var startDayOfWeek = (int)firstOfMonth.DayOfWeek;
            for (int i = 0; i < startDayOfWeek; i++)
                days.Add(new CalendarDayItem { IsBlank = true });

            for (int day = 1; day <= lastOfMonth.Day; day++)
            {
                var date = new DateTime(DisplayMonth.Year, DisplayMonth.Month, day);
                var workout = workouts.FirstOrDefault(w => w.ScheduledDate.Date == date.Date);

                var item = new CalendarDayItem
                {
                    Date = date,
                    DayNumber = day.ToString(),
                    IsToday = date.Date == DateTime.Today
                };

                if (workout != null)
                {
                    item.ScheduledWorkoutId = workout.Id;
                    item.Status = workout.Status;
                    item.StatusColor = workout.Status switch
                    {
                        WorkoutStatus.Completed => "#4CAF50",
                        WorkoutStatus.Scheduled => "#2196F3",
                        WorkoutStatus.Missed => "#F44336",
                        WorkoutStatus.Skipped => "#9E9E9E",
                        _ => "Transparent"
                    };
                    item.HasWorkout = true;
                }

                days.Add(item);
            }

            CalendarDays = new ObservableCollection<CalendarDayItem>(days);

            var (completed, missed, _, total) = await _scheduleService.GetCompletionStatsAsync(user.Id, 30);
            CompletedCount = completed;
            MissedCount = missed;
            ScheduledCount = total - completed - missed;
            CurrentStreak = await _scheduleService.GetStreakAsync(user.Id);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Calendar load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectDayAsync(CalendarDayItem day)
    {
        if (day.IsBlank) return;
        SelectedDay = day;
        HasSelectedDay = true;

        if (day.HasWorkout && day.ScheduledWorkoutId > 0)
        {
            var db = await _databaseService.GetConnectionAsync();
            var workout = await db.Table<ScheduledWorkout>()
                .FirstOrDefaultAsync(w => w.Id == day.ScheduledWorkoutId);

            if (workout?.WorkoutDayId != null)
            {
                var workoutDay = await db.Table<WorkoutDay>()
                    .FirstOrDefaultAsync(d => d.Id == workout.WorkoutDayId.Value);
                SelectedDayWorkoutName = workoutDay?.DayName ?? "Workout";
            }
            else
            {
                SelectedDayWorkoutName = "Workout";
            }

            SelectedDayStatus = day.Status.ToString();
            CanCompleteSelectedWorkout = day.Status == WorkoutStatus.Scheduled;
        }
        else
        {
            SelectedDayWorkoutName = "Rest Day";
            SelectedDayStatus = "No workout scheduled";
            CanCompleteSelectedWorkout = false;
        }
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        DisplayMonth = DisplayMonth.AddMonths(-1);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        DisplayMonth = DisplayMonth.AddMonths(1);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task CompleteWorkoutAsync()
    {
        if (SelectedDay?.ScheduledWorkoutId > 0)
            await Shell.Current.GoToAsync($"{RouteConstants.WorkoutComplete}?scheduledWorkoutId={SelectedDay.ScheduledWorkoutId}");
    }

    [RelayCommand]
    private async Task SkipWorkoutAsync()
    {
        if (SelectedDay?.ScheduledWorkoutId > 0)
        {
            bool confirmed = await Shell.Current.DisplayAlert("Skip Workout", "Skip this workout?", "Skip", "Cancel");
            if (confirmed)
            {
                await _scheduleService.SkipWorkoutAsync(SelectedDay.ScheduledWorkoutId);
                await LoadDataAsync();
            }
        }
    }
}

public partial class CalendarDayItem : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayNumber { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool IsBlank { get; set; }
    public bool HasWorkout { get; set; }
    public int ScheduledWorkoutId { get; set; }
    public WorkoutStatus Status { get; set; }
    public string StatusColor { get; set; } = "Transparent";
}

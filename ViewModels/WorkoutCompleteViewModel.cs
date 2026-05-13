using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

[QueryProperty(nameof(ScheduledWorkoutId), "scheduledWorkoutId")]
public partial class WorkoutCompleteViewModel : BaseViewModel
{
    private readonly IScheduleService _scheduleService;

    public WorkoutCompleteViewModel(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
        Title = "Workout Complete";
    }

    [ObservableProperty]
    private int _scheduledWorkoutId;

    [ObservableProperty]
    private string _durationMinutesText = string.Empty;

    [ObservableProperty]
    private int _difficultyRating = 3;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _estimatedCaloriesBurned = "0";

    [ObservableProperty]
    private string _difficultyLabel = "Moderate";

    [ObservableProperty]
    private bool _star1Filled = true;

    [ObservableProperty]
    private bool _star2Filled = true;

    [ObservableProperty]
    private bool _star3Filled = true;

    [ObservableProperty]
    private bool _star4Filled;

    [ObservableProperty]
    private bool _star5Filled;

    [ObservableProperty]
    private string _durationError = string.Empty;

    [ObservableProperty]
    private bool _hasDurationError;

    partial void OnDifficultyRatingChanged(int value)
    {
        Star1Filled = value >= 1;
        Star2Filled = value >= 2;
        Star3Filled = value >= 3;
        Star4Filled = value >= 4;
        Star5Filled = value >= 5;

        DifficultyLabel = value switch
        {
            1 => "Very Easy",
            2 => "Easy",
            3 => "Moderate",
            4 => "Hard",
            5 => "Very Hard",
            _ => "Moderate"
        };

        UpdateCaloriesEstimate();
    }

    partial void OnDurationMinutesTextChanged(string value)
    {
        UpdateCaloriesEstimate();
    }

    private void UpdateCaloriesEstimate()
    {
        if (int.TryParse(DurationMinutesText, out var minutes) && minutes > 0)
        {
            double caloriesPerMinute = DifficultyRating switch
            {
                1 => 4.0,
                2 => 5.5,
                3 => 7.0,
                4 => 9.0,
                5 => 11.0,
                _ => 7.0
            };

            int estimated = (int)(minutes * caloriesPerMinute);
            EstimatedCaloriesBurned = estimated.ToString();
        }
        else
        {
            EstimatedCaloriesBurned = "0";
        }
    }

    [RelayCommand]
    private void SetDifficulty(string rating)
    {
        if (int.TryParse(rating, out var value) && value >= 1 && value <= 5)
        {
            DifficultyRating = value;
        }
    }

    [RelayCommand]
    private async Task SaveCompletionAsync()
    {
        if (!ValidateForm()) return;

        IsBusy = true;
        try
        {
            int.TryParse(DurationMinutesText, out var duration);

            var completion = await _scheduleService.CompleteWorkoutAsync(
                ScheduledWorkoutId,
                duration,
                DifficultyRating,
                string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());

            if (int.TryParse(EstimatedCaloriesBurned, out var calories) && calories > 0)
            {
                completion.CaloriesBurned = calories;
            }

            await Shell.Current.DisplayAlert(
                "Great Work!",
                $"Workout completed in {duration} minutes. Estimated {EstimatedCaloriesBurned} calories burned!",
                "OK");

            await Shell.Current.GoToAsync("../..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to save workout: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ValidateForm()
    {
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(DurationMinutesText) ||
            !int.TryParse(DurationMinutesText, out var duration) ||
            duration <= 0 || duration > 300)
        {
            DurationError = "Please enter a valid duration (1-300 minutes)";
            HasDurationError = true;
            isValid = false;
        }
        else
        {
            DurationError = string.Empty;
            HasDurationError = false;
        }

        return isValid;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Discard Feedback",
            "Are you sure you want to skip logging this workout?",
            "Discard", "Continue");

        if (confirmed)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}

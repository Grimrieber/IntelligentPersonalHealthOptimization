using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Helpers;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IUserService _userService;
    private readonly ISecurityService _securityService;
    private readonly INotificationService _notificationService;
    private readonly INutritionService _nutritionService;
    private readonly IDatabaseService _databaseService;
    public SettingsViewModel(
        IUserService userService,
        ISecurityService securityService,
        INotificationService notificationService,
        INutritionService nutritionService,
        IDatabaseService databaseService)
    {
        _userService = userService;
        _securityService = securityService;
        _notificationService = notificationService;
        _nutritionService = nutritionService;
        _databaseService = databaseService;
        Title = "Settings";
    }

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userDetails = string.Empty;
    [ObservableProperty] private string _currentPin = string.Empty;
    [ObservableProperty] private string _newPin = string.Empty;
    [ObservableProperty] private string _confirmNewPin = string.Empty;
    [ObservableProperty] private string _pinMessage = string.Empty;
    [ObservableProperty] private bool _isChangingPin;

    // Notifications
    [ObservableProperty] private bool _notificationsEnabled;
    [ObservableProperty] private int _reminderMinutesBefore = 30;

    // Nutrition
    [ObservableProperty] private string _nutritionSummary = string.Empty;
    [ObservableProperty] private bool _hasNutritionProfile;

    // Training
    [ObservableProperty] private string _trainingSummary = string.Empty;
    [ObservableProperty] private bool _hasTrainingProfile;
    // CSV of training days, bound to the shared TrainingDaySelector control.
    [ObservableProperty] private string _trainingDaysCsv = string.Empty;

    // Edit Profile
    [ObservableProperty] private bool _isEditingProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditWeightLbDisplay))]
    private double _editWeightKg;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditHeightDisplay))]
    private double _editHeightCm;

    [ObservableProperty] private ActivityLevel _editActivityLevel;

    // Humanized wrapper so the Picker shows "Moderately Active" rather than the raw
    // concatenated enum name. EditActivityLevel stays the source of truth for Save.
    [ObservableProperty] private PickerItem<ActivityLevel>? _selectedActivityLevelOption;
    partial void OnSelectedActivityLevelOptionChanged(PickerItem<ActivityLevel>? value)
    {
        if (value != null) EditActivityLevel = value.Value;
    }

    [ObservableProperty] private string _profileMessage = string.Empty;

    public string EditWeightLbDisplay => $"{EditWeightKg * 2.20462:F0} lb";

    public string EditHeightDisplay
    {
        get
        {
            var totalInches = EditHeightCm / 2.54;
            var feet = (int)(totalInches / 12);
            var inches = (int)(totalInches % 12);
            return $"{EditHeightCm:F0} cm / {feet}'{inches}\"";
        }
    }

    public List<PickerItem<ActivityLevel>> ActivityLevelOptions { get; } =
        PickerItem<ActivityLevel>.From(Enum.GetValues<ActivityLevel>());

    private User? _currentUser;
    private bool _loadingSettings;

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        var user = await _userService.GetCurrentUserAsync();
        if (user == null) return;

        _currentUser = user;
        UserName = $"{user.FirstName} {user.LastName}";
        var age = DateTime.Today.Year - user.DateOfBirth.Year;
        if (user.DateOfBirth > DateTime.Today.AddYears(-age)) age--;
        var lbs = user.WeightKg * 2.20462;
        var totalInches = user.HeightCm / 2.54;
        var feet = (int)(totalInches / 12);
        var inches = (int)(totalInches % 12);
        var activityOption = ActivityLevelOptions.First(o => o.Value == user.ActivityLevel);
        UserDetails = $"Age {age} | {user.HeightCm:F0} cm / {feet}'{inches}\" | {user.WeightKg:F1} kg ({lbs:F0} lb) | {activityOption.Display}";

        // Pre-populate edit fields
        EditWeightKg = user.WeightKg;
        EditHeightCm = user.HeightCm;
        EditActivityLevel = user.ActivityLevel;
        SelectedActivityLevelOption = activityOption;

        // Load notification settings
        NotificationsEnabled = await _notificationService.AreNotificationsEnabledAsync();

        // Load nutrition profile summary
        var db = await _databaseService.GetConnectionAsync();
        var nutritionProfile = await db.Table<NutritionProfile>().FirstOrDefaultAsync(p => p.UserId == user.Id);
        HasNutritionProfile = nutritionProfile != null;
        if (nutritionProfile != null)
        {
            NutritionSummary = $"{nutritionProfile.DietType} | {nutritionProfile.TargetCalories} kcal | P:{nutritionProfile.TargetProteinG}g C:{nutritionProfile.TargetCarbsG}g F:{nutritionProfile.TargetFatG}g";
        }

        // Load training profile summary
        var trainingProfile = await db.Table<TrainingProfile>().FirstOrDefaultAsync(t => t.UserId == user.Id);
        HasTrainingProfile = trainingProfile != null;
        if (trainingProfile != null)
        {
            TrainingSummary = $"{trainingProfile.ExperienceLevel} | {trainingProfile.CurrentFrequency}x/week | {trainingProfile.SessionDurationMinutes} min";

            // Seed the day selector without triggering a save (guarded).
            _loadingSettings = true;
            var days = (trainingProfile.AvailableDays ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            TrainingDaysCsv = days.Length == 0 ? "Monday,Wednesday,Friday" : string.Join(",", days);
            _loadingSettings = false;
        }
    }

    partial void OnTrainingDaysCsvChanged(string value)
    {
        if (_loadingSettings) return;
        _ = SaveTrainingDaysAsync(value);
    }

    private async Task SaveTrainingDaysAsync(string daysCsv)
    {
        if (_currentUser == null) return;
        try
        {
            var count = daysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
            var db = await _databaseService.GetConnectionAsync();
            var profile = await db.Table<TrainingProfile>().FirstOrDefaultAsync(t => t.UserId == _currentUser.Id);
            if (profile == null) return;

            profile.AvailableDays = daysCsv;
            profile.CurrentFrequency = count;
            profile.UpdatedAt = DateTime.UtcNow;
            await _databaseService.UpdateAsync(profile);

            TrainingSummary = $"{profile.ExperienceLevel} | {profile.CurrentFrequency}x/week | {profile.SessionDurationMinutes} min";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Settings.SaveTrainingDays", ex);
        }
    }

    [RelayCommand]
    private void ToggleEditProfile()
    {
        IsEditingProfile = !IsEditingProfile;
        ProfileMessage = string.Empty;
        if (IsEditingProfile && _currentUser != null)
        {
            EditWeightKg = _currentUser.WeightKg;
            EditHeightCm = _currentUser.HeightCm;
            EditActivityLevel = _currentUser.ActivityLevel;
            SelectedActivityLevelOption = ActivityLevelOptions.First(o => o.Value == _currentUser.ActivityLevel);
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (_currentUser == null) return;

        _currentUser.WeightKg = EditWeightKg;
        _currentUser.HeightCm = EditHeightCm;
        _currentUser.ActivityLevel = EditActivityLevel;
        _currentUser.UpdatedAt = DateTime.UtcNow;
        await _databaseService.UpdateAsync(_currentUser);

        // Fully recalculate nutrition targets with updated weight/height/activity
        var db = await _databaseService.GetConnectionAsync();
        var profile = await db.Table<NutritionProfile>().FirstOrDefaultAsync(p => p.UserId == _currentUser.Id);
        if (profile != null)
        {
            // Recompute through the single shared entry point. It pulls ALL goal inputs
            // (incl. focus areas / confidence / readiness) off the latest assessment, so a
            // weight edit here produces exactly the macros the assessment did. When there's
            // no assessment it falls back to the FitnessGoal-based path.
            var assessment = await _nutritionService.GetLatestAssessmentAsync(_currentUser.Id);
            var t = _nutritionService.ComputeTargetsForUser(_currentUser, assessment);

            profile.TargetCalories = t.Calories;
            profile.TargetProteinG = t.ProteinG;
            profile.TargetCarbsG = t.CarbsG;
            profile.TargetFatG = t.FatG;
            profile.BMR = t.Bmr;
            profile.TDEE = t.Tdee;
            profile.UpdatedAt = DateTime.UtcNow;
            await _databaseService.UpdateAsync(profile);
        }

        IsEditingProfile = false;
        ProfileMessage = "Profile updated! Nutrition targets recalculated.";

        // Refresh displayed info
        await LoadSettingsAsync();
    }

    [RelayCommand]
    private void ToggleChangePin()
    {
        IsChangingPin = !IsChangingPin;
        PinMessage = string.Empty;
        CurrentPin = string.Empty;
        NewPin = string.Empty;
        ConfirmNewPin = string.Empty;
    }

    [RelayCommand]
    private async Task ChangePinAsync()
    {
        PinMessage = string.Empty;

        if (!await _securityService.ValidatePinAsync(CurrentPin))
        {
            PinMessage = "Current PIN is incorrect.";
            return;
        }

        if (!ValidationHelper.IsValidPin(NewPin))
        {
            PinMessage = $"New PIN must be {AppConstants.MinPinLength}-{AppConstants.MaxPinLength} digits.";
            return;
        }

        if (NewPin != ConfirmNewPin)
        {
            PinMessage = "New PINs do not match.";
            return;
        }

        await _securityService.SetPinAsync(NewPin);
        PinMessage = "PIN changed successfully!";
        IsChangingPin = false;
    }

    [RelayCommand]
    private async Task ToggleNotificationsAsync()
    {
        if (NotificationsEnabled)
        {
            await _notificationService.CancelAllRemindersAsync();
            NotificationsEnabled = false;
        }
        else
        {
            NotificationsEnabled = true;
        }
    }

    [RelayCommand]
    private async Task RegenerateMealPlanAsync()
    {
        var confirmed = await Shell.Current.DisplayAlert(
            "Regenerate Meal Plan",
            "This will create a new 7-day meal plan based on your current nutrition profile. Your food log history will be preserved.",
            "Regenerate", "Cancel");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var db = await _databaseService.GetConnectionAsync();

            // Clear today's food log so calories/macros reset
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            await db.ExecuteAsync(
                "DELETE FROM FoodLogEntry WHERE UserId = ? AND LogDate >= ? AND LogDate < ?",
                user.Id, today, tomorrow);

            var profile = await db.Table<NutritionProfile>().FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile != null)
            {
                try
                {
                    await _nutritionService.GenerateRecipeMealPlanAsync(user.Id, profile.Id);
                }
                catch
                {
                    await _nutritionService.RegenerateMealPlanAsync(user.Id, profile.Id);
                }
                await Shell.Current.DisplayAlert("Success", "Your meal plan has been regenerated!", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to regenerate: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ViewGoalsAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.GoalsOverview);
    }

    [RelayCommand]
    private async Task ViewCalendarAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.Calendar);
    }

    [RelayCommand]
    private async Task UpdateEquipmentAsync()
    {
        await Shell.Current.GoToAsync($"{RouteConstants.EquipmentIntro}?reentry=true");
    }

    [RelayCommand]
    private async Task EditWorkingWeightsAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.WorkingWeights);
    }

    [RelayCommand]
    private async Task ClearAllDataAsync()
    {
        var confirmed = await Shell.Current.DisplayAlert(
            "Clear All Data",
            "This will permanently delete all your data including your profile, assessments, workout programs, nutrition data, and progress history. This cannot be undone.",
            "Delete Everything",
            "Cancel");

        if (!confirmed) return;

        await _notificationService.CancelAllRemindersAsync();
        SecureStorageHelper.RemoveAll();
        await Shell.Current.GoToAsync($"//{RouteConstants.Login}");
    }

    [RelayCommand]
    private async Task ViewCrashLogAsync()
    {
        var log = Services.CrashLogger.ReadLastEntries(20);
        await Shell.Current.DisplayAlert("Error Log", log, "OK");
    }

    [RelayCommand]
    private async Task ShareCrashLogAsync()
    {
        var logPath = Services.CrashLogger.GetLogFilePath();
        if (!File.Exists(logPath))
        {
            await Shell.Current.DisplayAlert("Error Log", "No crash log entries found.", "OK");
            return;
        }

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Share Error Log",
            File = new ShareFile(logPath)
        });
    }

    [RelayCommand]
    private async Task ClearCrashLogAsync()
    {
        var confirmed = await Shell.Current.DisplayAlert("Clear Error Log",
            "Are you sure you want to clear the error log?", "Clear", "Cancel");
        if (confirmed)
        {
            Services.CrashLogger.ClearLog();
            await Shell.Current.DisplayAlert("Done", "Error log cleared.", "OK");
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        App.EndSession();
        await Shell.Current.GoToAsync($"//{RouteConstants.Login}");
    }
}

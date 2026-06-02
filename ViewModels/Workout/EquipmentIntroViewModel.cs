using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Helpers;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Workout;

[QueryProperty(nameof(SessionId), "sessionId")]
[QueryProperty(nameof(IsReentry), "reentry")]
public partial class EquipmentIntroViewModel : BaseViewModel
{
    private readonly IEquipmentIntelService _intelService;
    private readonly IUserService _userService;
    private readonly IDatabaseService _databaseService;
    private int _userId;
    private bool _isInitializing;

    public EquipmentIntroViewModel(
        IEquipmentIntelService intelService,
        IUserService userService,
        IDatabaseService databaseService)
    {
        _intelService = intelService;
        _userService = userService;
        _databaseService = databaseService;
        Title = "Equipment & Environment";
    }

    [ObservableProperty] private int _sessionId;
    [ObservableProperty] private bool _isReentry;
    [ObservableProperty] private string _summaryLine = string.Empty;

    [ObservableProperty] private PickerItem<TrainingLocation>? _selectedLocation;
    [ObservableProperty] private PickerItem<FitnessGoal>? _selectedGoal;
    [ObservableProperty] private PickerItem<ExperienceLevel>? _selectedExperienceLevel;
    // CSV of selected weekday names — bound to the TrainingDaySelector control, which also
    // enforces the 2–6 range. The day count IS the weekly frequency (dual purpose).
    [ObservableProperty] private string _availableDaysCsv = string.Empty;
    [ObservableProperty] private int _selectedSessionMinutes = 60;
    [ObservableProperty] private bool _hasKneeIssue;
    [ObservableProperty] private bool _hasShoulderIssue;
    [ObservableProperty] private bool _hasBackIssue;
    [ObservableProperty] private string _saveStatus = string.Empty;
    [ObservableProperty] private bool _hasExistingInventory;

    public List<int> SessionMinutesOptions { get; } = [20, 30, 45, 60, 75, 90, 120];

    public List<PickerItem<TrainingLocation>> LocationOptions { get; } =
        PickerItem<TrainingLocation>.From(
            [TrainingLocation.Gym, TrainingLocation.Home, TrainingLocation.Both]);

    public List<PickerItem<FitnessGoal>> GoalOptions { get; } =
        PickerItem<FitnessGoal>.From(
            [FitnessGoal.GeneralFitness, FitnessGoal.WeightLoss, FitnessGoal.MuscleBuilding,
             FitnessGoal.ImprovedMobility, FitnessGoal.Endurance, FitnessGoal.Rehabilitation]);

    public List<PickerItem<ExperienceLevel>> ExperienceLevelOptions { get; } =
        PickerItem<ExperienceLevel>.From(
            [ExperienceLevel.Beginner, ExperienceLevel.Novice, ExperienceLevel.Intermediate,
             ExperienceLevel.Advanced, ExperienceLevel.Elite]);

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        _isInitializing = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;
            _userId = user.Id;

            var ctx = await _intelService.LoadContextAsync(_userId);

            HasExistingInventory = ctx.ExistingInventory.Count > 0;

            if (SessionId == 0 && ctx.LatestCes != null)
                SessionId = ctx.LatestCes.AssessmentSessionId;

            SummaryLine = IsReentry
                ? "Update your equipment, location, and goal. Changes save as you make them. Your existing program won't change until you tap 'Regenerate' at the end."
                : "We need 2 minutes of detail about your equipment to design your program. Edit anything below — changes save automatically.";

            SelectedLocation = LocationOptions.FirstOrDefault(o => o.Value.Equals(ctx.TrainingLocation)) ?? LocationOptions[0];
            SelectedGoal = GoalOptions.FirstOrDefault(o => o.Value.Equals(ctx.FitnessGoal)) ?? GoalOptions[0];
            SelectedExperienceLevel = ExperienceLevelOptions.FirstOrDefault(o => o.Value.Equals(ctx.ExperienceLevel)) ?? ExperienceLevelOptions[0];
            AvailableDaysCsv = NormalizeDaysCsv(ctx.TrainingProfile?.AvailableDays);
            SelectedSessionMinutes = ctx.SessionDurationMinutes > 0 ? ctx.SessionDurationMinutes : 60;
            HasKneeIssue = ctx.HasKneeIssue;
            HasShoulderIssue = ctx.HasShoulderIssue;
            HasBackIssue = ctx.HasBackIssue;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EquipmentIntro.Load", ex);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
        }
    }

    partial void OnSelectedLocationChanged(PickerItem<TrainingLocation>? value)
    {
        if (_isInitializing || value == null) return;
        _ = SaveLocationAsync(value.Value);
    }

    partial void OnSelectedGoalChanged(PickerItem<FitnessGoal>? value)
    {
        if (_isInitializing || value == null) return;
        _ = SaveGoalAsync(value.Value);
    }

    partial void OnSelectedExperienceLevelChanged(PickerItem<ExperienceLevel>? value)
    {
        if (_isInitializing || value == null) return;
        _ = SaveExperienceLevelAsync(value.Value);
    }

    partial void OnSelectedSessionMinutesChanged(int value)
    {
        if (_isInitializing) return;
        _ = SaveSessionMinutesAsync(value);
    }

    partial void OnAvailableDaysCsvChanged(string value)
    {
        if (_isInitializing) return;
        _ = SaveDaysAsync(value);
    }

    private async Task SaveLocationAsync(TrainingLocation value)
    {
        try
        {
            var db = await _databaseService.GetConnectionAsync();
            var profile = await db.Table<TrainingProfile>().FirstOrDefaultAsync(t => t.UserId == _userId);

            if (profile == null)
            {
                profile = new TrainingProfile { UserId = _userId, TrainingLocation = value };
                await db.InsertAsync(profile);
            }
            else
            {
                profile.TrainingLocation = value;
                profile.UpdatedAt = DateTime.UtcNow;
                await db.UpdateAsync(profile);
            }

            SaveStatus = $"Training location updated to {value}";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EquipmentIntro.SaveLocation", ex);
            SaveStatus = "Could not save — try again.";
        }
    }

    private async Task SaveGoalAsync(FitnessGoal value)
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            user.FitnessGoal = value;
            user.UpdatedAt = DateTime.UtcNow;
            await _userService.UpdateUserAsync(user);

            SaveStatus = $"Goal updated to {value}";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EquipmentIntro.SaveGoal", ex);
            SaveStatus = "Could not save — try again.";
        }
    }

    private async Task SaveExperienceLevelAsync(ExperienceLevel value)
    {
        try
        {
            var db = await _databaseService.GetConnectionAsync();
            var profile = await db.Table<TrainingProfile>().FirstOrDefaultAsync(t => t.UserId == _userId);

            if (profile == null)
            {
                profile = new TrainingProfile { UserId = _userId, ExperienceLevel = value };
                await db.InsertAsync(profile);
            }
            else
            {
                profile.ExperienceLevel = value;
                profile.UpdatedAt = DateTime.UtcNow;
                await db.UpdateAsync(profile);
            }

            SaveStatus = $"Workout level updated to {value}";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EquipmentIntro.SaveExperienceLevel", ex);
            SaveStatus = "Could not save — try again.";
        }
    }

    private async Task SaveSessionMinutesAsync(int value)
    {
        try
        {
            var db = await _databaseService.GetConnectionAsync();
            var profile = await db.Table<TrainingProfile>().FirstOrDefaultAsync(t => t.UserId == _userId);

            if (profile == null)
            {
                profile = new TrainingProfile { UserId = _userId, SessionDurationMinutes = value };
                await db.InsertAsync(profile);
            }
            else
            {
                profile.SessionDurationMinutes = value;
                profile.UpdatedAt = DateTime.UtcNow;
                await db.UpdateAsync(profile);
            }

            SaveStatus = $"Session duration updated to {value} minutes";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EquipmentIntro.SaveSessionMinutes", ex);
            SaveStatus = "Could not save — try again.";
        }
    }

    // Normalize a stored AvailableDays CSV; fall back to Mon/Wed/Fri when nothing is saved yet
    // so the selector always opens on a valid (in-range) selection.
    private static string NormalizeDaysCsv(string? availableDaysCsv)
    {
        var days = (availableDaysCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return days.Length == 0 ? "Monday,Wednesday,Friday" : string.Join(",", days);
    }

    private async Task SaveDaysAsync(string daysCsv)
    {
        try
        {
            var count = daysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;

            var db = await _databaseService.GetConnectionAsync();
            var profile = await db.Table<TrainingProfile>().FirstOrDefaultAsync(t => t.UserId == _userId);

            if (profile == null)
            {
                profile = new TrainingProfile
                {
                    UserId = _userId,
                    CurrentFrequency = count,
                    AvailableDays = daysCsv
                };
                await db.InsertAsync(profile);
            }
            else
            {
                profile.CurrentFrequency = count;
                profile.AvailableDays = daysCsv;
                profile.UpdatedAt = DateTime.UtcNow;
                await db.UpdateAsync(profile);
            }

            SaveStatus = $"Training days updated — {count} days/week";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EquipmentIntro.SaveDays", ex);
            SaveStatus = "Could not save — try again.";
        }
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        await Shell.Current.GoToAsync($"{RouteConstants.EquipmentDetail}?sessionId={SessionId}&reentry={IsReentry}");
    }

    [RelayCommand]
    private async Task SkipAsync()
    {
        await Shell.Current.GoToAsync($"{RouteConstants.ProgramPreview}?sessionId={SessionId}&skipped=true");
    }

    partial void OnHasKneeIssueChanged(bool value) { if (!_isInitializing) _ = SaveInjuryFlagsAsync(); }
    partial void OnHasShoulderIssueChanged(bool value) { if (!_isInitializing) _ = SaveInjuryFlagsAsync(); }
    partial void OnHasBackIssueChanged(bool value) { if (!_isInitializing) _ = SaveInjuryFlagsAsync(); }

    private async Task SaveInjuryFlagsAsync()
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            // Preserve any other injury notes already in the field; just toggle the three flags by
            // re-writing the canonical list.
            var existing = (user.InjuryAreas ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !s.Equals("knee", StringComparison.OrdinalIgnoreCase)
                         && !s.Equals("shoulder", StringComparison.OrdinalIgnoreCase)
                         && !s.Equals("back", StringComparison.OrdinalIgnoreCase)
                         && !s.Equals("lumbar", StringComparison.OrdinalIgnoreCase)
                         && !s.Equals("spine", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (HasKneeIssue) existing.Add("knee");
            if (HasShoulderIssue) existing.Add("shoulder");
            if (HasBackIssue) existing.Add("back");

            user.InjuryAreas = string.Join(", ", existing);
            user.UpdatedAt = DateTime.UtcNow;
            await _userService.UpdateUserAsync(user);

            SaveStatus = "Injury flags updated";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EquipmentIntro.SaveInjuryFlags", ex);
            SaveStatus = "Could not save — try again.";
        }
    }
}

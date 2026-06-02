using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class AddProgressEntryViewModel : BaseViewModel
{
    private readonly IProgressService _progressService;
    private readonly IUserService _userService;
    private readonly IDatabaseService _databaseService;
    private readonly INutritionService _nutritionService;
    private bool _isSyncingWeight;

    public AddProgressEntryViewModel(
        IProgressService progressService,
        IUserService userService,
        IDatabaseService databaseService,
        INutritionService nutritionService)
    {
        _progressService = progressService;
        _userService = userService;
        _databaseService = databaseService;
        _nutritionService = nutritionService;
        Title = "Log Progress";
    }

    [ObservableProperty] private string _weightKg = string.Empty;
    [ObservableProperty] private string _weightLb = string.Empty;
    [ObservableProperty] private string _chestCm = string.Empty;
    [ObservableProperty] private string _waistCm = string.Empty;
    [ObservableProperty] private string _hipsCm = string.Empty;
    [ObservableProperty] private string _leftArmCm = string.Empty;
    [ObservableProperty] private string _rightArmCm = string.Empty;
    [ObservableProperty] private string _leftThighCm = string.Empty;
    [ObservableProperty] private string _rightThighCm = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // Sync kg ↔ lb
    partial void OnWeightKgChanged(string value)
    {
        if (_isSyncingWeight) return;
        if (double.TryParse(value, out var kg) && kg > 0)
        {
            _isSyncingWeight = true;
            WeightLb = $"{kg * 2.20462:F1}";
            _isSyncingWeight = false;
        }
    }

    partial void OnWeightLbChanged(string value)
    {
        if (_isSyncingWeight) return;
        if (double.TryParse(value, out var lb) && lb > 0)
        {
            _isSyncingWeight = true;
            WeightKg = $"{lb / 2.20462:F1}";
            _isSyncingWeight = false;
        }
    }

    [RelayCommand]
    private async Task LoadCurrentAsync()
    {
        var user = await _userService.GetCurrentUserAsync();
        if (user != null && user.WeightKg > 0)
        {
            _isSyncingWeight = true;
            WeightKg = $"{user.WeightKg:F1}";
            WeightLb = $"{user.WeightKg * 2.20462:F1}";
            _isSyncingWeight = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null)
            {
                ErrorMessage = "No user found.";
                return;
            }

            double? weight = null;
            if (!string.IsNullOrEmpty(WeightKg) && double.TryParse(WeightKg, out var w) && w > 0)
                weight = w;

            var measurements = new List<(string name, double valueCm)>();
            AddMeasurement(measurements, "Chest", ChestCm);
            AddMeasurement(measurements, "Waist", WaistCm);
            AddMeasurement(measurements, "Hips", HipsCm);
            AddMeasurement(measurements, "Left Arm", LeftArmCm);
            AddMeasurement(measurements, "Right Arm", RightArmCm);
            AddMeasurement(measurements, "Left Thigh", LeftThighCm);
            AddMeasurement(measurements, "Right Thigh", RightThighCm);

            if (weight == null && measurements.Count == 0)
            {
                ErrorMessage = "Please enter at least a weight or one measurement.";
                return;
            }

            // Log the progress entry
            await _progressService.LogProgressAsync(user.Id, weight, measurements, string.Empty);

            // Update the User record with the new weight and recalculate nutrition
            if (weight.HasValue && Math.Abs(weight.Value - user.WeightKg) > 0.01)
            {
                user.WeightKg = weight.Value;
                user.UpdatedAt = DateTime.UtcNow;
                await _databaseService.UpdateAsync(user);

                // Recalculate nutrition targets
                var db = await _databaseService.GetConnectionAsync();
                var profile = await db.Table<NutritionProfile>()
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (profile != null)
                {
                    // Shared entry point: pulls every goal input off the latest assessment
                    // so a new-weight recompute matches the assessment's own macros.
                    var assessment = await _nutritionService.GetLatestAssessmentAsync(user.Id);
                    var t = _nutritionService.ComputeTargetsForUser(user, assessment);

                    if (assessment != null)
                    {
                        profile.TargetCalories = t.Calories;
                        profile.TargetProteinG = t.ProteinG;
                        profile.TargetCarbsG = t.CarbsG;
                        profile.TargetFatG = t.FatG;
                    }

                    profile.BMR = t.Bmr;
                    profile.TDEE = t.Tdee;
                    profile.UpdatedAt = DateTime.UtcNow;
                    await _databaseService.UpdateAsync(profile);
                }
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void AddMeasurement(List<(string, double)> list, string name, string value)
    {
        if (!string.IsNullOrEmpty(value) && double.TryParse(value, out var v) && v > 0)
            list.Add((name, v));
    }
}

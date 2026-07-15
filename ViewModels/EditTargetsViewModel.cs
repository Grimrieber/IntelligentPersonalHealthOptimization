using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

/// <summary>
/// Lets the user hand-set their daily calorie + macro targets. Saving flips
/// <see cref="NutritionProfile.UseManualTargets"/> so the dashboard stops
/// recomputing from the assessment; "Reset to automatic" flips it back and the
/// dashboard recomputes on next load. Live-shows the macro calorie total so the
/// user can see whether protein/carbs/fat add up to their calorie goal.
/// </summary>
public partial class EditTargetsViewModel : BaseViewModel
{
    private readonly IUserService _userService;
    private readonly INutritionService _nutritionService;
    private readonly IDatabaseService _databaseService;

    private NutritionProfile? _profile;

    public EditTargetsViewModel(
        IUserService userService,
        INutritionService nutritionService,
        IDatabaseService databaseService)
    {
        _userService = userService;
        _nutritionService = nutritionService;
        _databaseService = databaseService;
        Title = "Adjust Targets";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MacroCalorieSummary))]
    private string _calories = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MacroCalorieSummary))]
    private string _protein = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MacroCalorieSummary))]
    private string _carbs = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MacroCalorieSummary))]
    private string _fat = string.Empty;

    [ObservableProperty]
    private string _waterGlasses = string.Empty;

    [ObservableProperty]
    private bool _isManual;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>"Macros add up to 1,980 kcal" — protein·4 + carbs·4 + fat·9 — so the
    /// user can reconcile the macro split against the calorie goal.</summary>
    public string MacroCalorieSummary
    {
        get
        {
            var kcal = ParseInt(Protein) * 4 + ParseInt(Carbs) * 4 + ParseInt(Fat) * 9;
            return kcal > 0 ? $"Macros add up to {kcal:N0} kcal" : string.Empty;
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            _profile = user != null ? await _nutritionService.GetNutritionProfileAsync(user.Id) : null;
            if (_profile == null) return;

            Calories = _profile.TargetCalories.ToString();
            Protein = _profile.TargetProteinG.ToString();
            Carbs = _profile.TargetCarbsG.ToString();
            Fat = _profile.TargetFatG.ToString();
            WaterGlasses = _profile.DailyWaterGlasses.ToString();
            IsManual = _profile.UseManualTargets;
            StatusText = IsManual
                ? "Your targets are set manually."
                : "Your targets are calculated automatically from your assessment.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_profile == null) return;

        var cal = ParseInt(Calories);
        var p = ParseInt(Protein);
        var c = ParseInt(Carbs);
        var f = ParseInt(Fat);

        if (cal < 800 || cal > 8000)
        {
            await Shell.Current.DisplayAlert("Check calories",
                "Enter a daily calorie target between 800 and 8000.", "OK");
            return;
        }
        if (p < 0 || c < 0 || f < 0)
        {
            await Shell.Current.DisplayAlert("Check macros",
                "Protein, carbs and fat can't be negative.", "OK");
            return;
        }

        _profile.TargetCalories = cal;
        _profile.TargetProteinG = p;
        _profile.TargetCarbsG = c;
        _profile.TargetFatG = f;
        var water = ParseInt(WaterGlasses);
        if (water is >= 1 and <= 30) _profile.DailyWaterGlasses = water;
        _profile.UseManualTargets = true;
        _profile.UpdatedAt = DateTime.UtcNow;
        await _databaseService.UpdateAsync(_profile);

        await Shell.Current.DisplayAlert("Saved",
            "Your targets are now set manually. The dashboard will use these numbers.", "OK");
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>Hand control back to the automatic calculation.</summary>
    [RelayCommand]
    private async Task ResetToAutoAsync()
    {
        if (_profile == null) return;

        var confirm = await Shell.Current.DisplayAlert("Reset to automatic",
            "Go back to targets calculated from your assessment? Your manual numbers will be replaced.",
            "Reset", "Cancel");
        if (!confirm) return;

        // Water goal is independent of the calorie/macro auto-calc — keep the
        // user's value even when handing macros back to automatic.
        var water = ParseInt(WaterGlasses);
        if (water is >= 1 and <= 30) _profile.DailyWaterGlasses = water;
        _profile.UseManualTargets = false;
        _profile.UpdatedAt = DateTime.UtcNow;
        await _databaseService.UpdateAsync(_profile);
        await Shell.Current.GoToAsync("..");
    }

    private static int ParseInt(string? s) =>
        int.TryParse(s?.Trim(), out var v) ? v : 0;
}

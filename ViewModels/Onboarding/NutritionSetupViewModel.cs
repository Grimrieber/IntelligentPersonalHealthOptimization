using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Helpers;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class NutritionSetupViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;
    private readonly INutritionService _nutritionService;

    public NutritionSetupViewModel(IOnboardingCoordinator coordinator, INutritionService nutritionService)
    {
        _coordinator = coordinator;
        _nutritionService = nutritionService;
        Title = _coordinator.StepTitle;

        _selectedDietType = DietTypeOptions.FirstOrDefault(o => o.Value == _coordinator.Data.DietType) ?? DietTypeOptions[0];
        _mealsPerDay = _coordinator.Data.MealsPerDay;
        _foodPreferences = _coordinator.Data.FoodPreferences;
        _foodDislikes = _coordinator.Data.FoodDislikes;
        _dailyWaterGlasses = _coordinator.Data.DailyWaterGlasses;
        _supplementUse = _coordinator.Data.SupplementUse;
        _selectedAlcoholFrequency = _coordinator.Data.AlcoholFrequency;
        _caffeinePerDay = _coordinator.Data.CaffeinePerDay;

        // Load allergy selections
        foreach (var allergy in _coordinator.Data.Allergies)
            SelectedAllergies.Add(allergy);

        // Calculate nutrition summary from earlier wizard steps
        CalculateNutritionSummary();
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<PickerItem<DietType>> DietTypeOptions { get; } =
        PickerItem<DietType>.From(Enum.GetValues<DietType>());
    public List<string> AlcoholFrequencyOptions { get; } = ["None", "Rarely", "Weekly", "Daily"];
    public List<FoodAllergy> AllAllergyOptions { get; } = Enum.GetValues<FoodAllergy>().ToList();

    [ObservableProperty]
    private PickerItem<DietType> _selectedDietType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MealsPerDayDisplay))]
    private int _mealsPerDay;

    [ObservableProperty]
    private string _foodPreferences = string.Empty;

    [ObservableProperty]
    private string _foodDislikes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaterDisplay))]
    private int _dailyWaterGlasses;

    [ObservableProperty]
    private string _supplementUse = string.Empty;

    [ObservableProperty]
    private string _selectedAlcoholFrequency;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaffeineDisplay))]
    private int _caffeinePerDay;

    public string MealsPerDayDisplay => $"{MealsPerDay} meals/day";
    public string WaterDisplay => $"{DailyWaterGlasses} glasses/day";
    public string CaffeineDisplay => $"{CaffeinePerDay} servings/day";

    public ObservableCollection<FoodAllergy> SelectedAllergies { get; } = [];

    // Nutrition Assessment Summary
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BMRDisplay))]
    private double _estimatedBMR;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TDEEDisplay))]
    private double _estimatedTDEE;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaloriesDisplay))]
    private int _recommendedCalories;

    [ObservableProperty]
    private int _recommendedProteinG;

    [ObservableProperty]
    private int _recommendedCarbsG;

    [ObservableProperty]
    private int _recommendedFatG;

    [ObservableProperty]
    private bool _hasSummaryData;

    public string BMRDisplay => $"{EstimatedBMR:F0} kcal";
    public string TDEEDisplay => $"{EstimatedTDEE:F0} kcal";
    public string CaloriesDisplay => $"{RecommendedCalories} kcal/day";

    partial void OnSelectedDietTypeChanged(PickerItem<DietType> value) => _coordinator.Data.DietType = value.Value;
    partial void OnMealsPerDayChanged(int value) => _coordinator.Data.MealsPerDay = value;
    partial void OnFoodPreferencesChanged(string value) => _coordinator.Data.FoodPreferences = value ?? string.Empty;
    partial void OnFoodDislikesChanged(string value) => _coordinator.Data.FoodDislikes = value ?? string.Empty;
    partial void OnDailyWaterGlassesChanged(int value) => _coordinator.Data.DailyWaterGlasses = value;
    partial void OnSupplementUseChanged(string value) => _coordinator.Data.SupplementUse = value ?? string.Empty;
    partial void OnSelectedAlcoholFrequencyChanged(string value) => _coordinator.Data.AlcoholFrequency = value ?? "None";
    partial void OnCaffeinePerDayChanged(int value) => _coordinator.Data.CaffeinePerDay = value;

    private void CalculateNutritionSummary()
    {
        var data = _coordinator.Data;

        if (data.WeightKg <= 0 || data.HeightCm <= 0)
        {
            HasSummaryData = false;
            return;
        }

        var tempUser = new User
        {
            DateOfBirth = data.DateOfBirth,
            Gender = data.Gender,
            HeightCm = data.HeightCm,
            WeightKg = data.WeightKg,
            ActivityLevel = data.ActivityLevel,
            FitnessGoal = data.PrimaryFitnessGoal
        };

        // No assessment during onboarding — shared entry point uses the FitnessGoal path,
        // so this preview matches the profile that CompleteOnboarding will create.
        var t = _nutritionService.ComputeTargetsForUser(tempUser, null);

        EstimatedBMR = t.Bmr;
        EstimatedTDEE = t.Tdee;
        RecommendedCalories = t.Calories;
        RecommendedProteinG = t.ProteinG;
        RecommendedCarbsG = t.CarbsG;
        RecommendedFatG = t.FatG;
        HasSummaryData = true;
    }

    [RelayCommand]
    private async Task AddAllergyAsync()
    {
        var unselected = AllAllergyOptions
            .Where(a => !SelectedAllergies.Contains(a))
            .Select(a => a.ToString()).ToArray();

        if (unselected.Length == 0) return;

        var result = await Shell.Current.DisplayActionSheet("Select Allergy", "Cancel", null, unselected);
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        if (Enum.TryParse<FoodAllergy>(result, out var allergy))
        {
            if (allergy == FoodAllergy.None)
            {
                SelectedAllergies.Clear();
                SelectedAllergies.Add(FoodAllergy.None);
            }
            else
            {
                SelectedAllergies.Remove(FoodAllergy.None);
                if (!SelectedAllergies.Contains(allergy))
                    SelectedAllergies.Add(allergy);
            }
        }

        SyncAllergies();
    }

    [RelayCommand]
    private void RemoveAllergy(FoodAllergy allergy)
    {
        SelectedAllergies.Remove(allergy);
        SyncAllergies();
    }

    private void SyncAllergies()
    {
        _coordinator.Data.Allergies = [.. SelectedAllergies];
    }

    [RelayCommand]
    private void IncrementMeals()
    {
        if (MealsPerDay < 6) MealsPerDay++;
    }

    [RelayCommand]
    private void DecrementMeals()
    {
        if (MealsPerDay > 2) MealsPerDay--;
    }

    [RelayCommand]
    private void IncrementWater()
    {
        if (DailyWaterGlasses < 15) DailyWaterGlasses++;
    }

    [RelayCommand]
    private void DecrementWater()
    {
        if (DailyWaterGlasses > 1) DailyWaterGlasses--;
    }

    [RelayCommand]
    private void IncrementCaffeine()
    {
        if (CaffeinePerDay < 10) CaffeinePerDay++;
    }

    [RelayCommand]
    private void DecrementCaffeine()
    {
        if (CaffeinePerDay > 0) CaffeinePerDay--;
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }
}

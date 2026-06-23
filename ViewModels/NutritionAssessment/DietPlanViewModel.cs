using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class DietPlanViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;

    private static readonly string[] AvoidableFoods =
    [
        "Spicy food", "Organ meats", "Raw fish", "Tofu / Tempeh",
        "Mushrooms", "Olives", "Pickled foods", "Strong-flavored cheese",
        "Artificial sweeteners", "Protein shakes", "Shellfish", "Pork",
        "Red meat", "Soy products", "Corn", "Nightshades (tomatoes, peppers)"
    ];

    public DietPlanViewModel(INutritionAssessmentCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        _selectedDietType = _coordinator.Data.SelectedDietType;
        _mealsPerDay = _coordinator.Data.MealsPerDay;
        _usesProteinShakes = _coordinator.Data.UsesProteinShakes;
        _shakesPerDay = _coordinator.Data.ShakesPerDay;
        _proteinPerShakeG = _coordinator.Data.ProteinPerShakeG;
        _dailyWaterGlasses = _coordinator.Data.DailyWaterGlasses;
        _mealPrepMode = _coordinator.Data.MealPrepMode;
        _mealPrepDays = _coordinator.Data.MealPrepDays;

        foreach (var allergy in _coordinator.Data.SelectedAllergies)
            SelectedAllergies.Add(allergy);

        foreach (var food in _coordinator.Data.FoodsToAvoid)
            FoodsToAvoid.Add(food);
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<DietType> DietTypeOptions => Enum.GetValues<DietType>().ToList();
    public List<FoodAllergy> AllAllergyOptions { get; } = Enum.GetValues<FoodAllergy>().ToList();

    [ObservableProperty]
    private DietType _selectedDietType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MealsPerDayDisplay))]
    private int _mealsPerDay;

    public string MealsPerDayDisplay => $"{MealsPerDay} meals/day";

    [ObservableProperty]
    private bool _usesProteinShakes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShakesPerDayDisplay))]
    private int _shakesPerDay;

    public string ShakesPerDayDisplay => $"{ShakesPerDay} shake{(ShakesPerDay != 1 ? "s" : "")}/day";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProteinPerShakeDisplay))]
    private int _proteinPerShakeG;

    public string ProteinPerShakeDisplay => $"{ProteinPerShakeG}g protein/shake";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaterGlassesDisplay))]
    private int _dailyWaterGlasses;

    public string WaterGlassesDisplay => $"{DailyWaterGlasses} glasses/day";

    [ObservableProperty]
    private bool _mealPrepMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MealPrepDaysDisplay))]
    private int _mealPrepDays;

    public string MealPrepDaysDisplay => $"Cook every {MealPrepDays} day{(MealPrepDays != 1 ? "s" : "")}";

    public ObservableCollection<FoodAllergy> SelectedAllergies { get; } = [];
    public ObservableCollection<string> FoodsToAvoid { get; } = [];

    partial void OnSelectedDietTypeChanged(DietType value) =>
        _coordinator.Data.SelectedDietType = value;

    [RelayCommand]
    private async Task AddAllergyAsync()
    {
        var unselected = AllAllergyOptions
            .Where(a => !SelectedAllergies.Contains(a))
            .Select(a => Helpers.EnumDisplay.Humanize(a.ToString())).ToArray();
        if (unselected.Length == 0) return;

        var result = await Shell.Current.DisplayActionSheet("Select Allergy", "Cancel", null, unselected);
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        if (Enum.TryParse<FoodAllergy>(result.Replace(" ", ""), out var allergy))
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
    }

    [RelayCommand]
    private void RemoveAllergy(FoodAllergy allergy) => SelectedAllergies.Remove(allergy);

    [RelayCommand]
    private async Task AddFoodToAvoidAsync()
    {
        var unselected = AvoidableFoods
            .Where(f => !FoodsToAvoid.Contains(f))
            .ToArray();
        if (unselected.Length == 0) return;

        var result = await Shell.Current.DisplayActionSheet("Select Food to Avoid", "Cancel", null, unselected);
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        if (!FoodsToAvoid.Contains(result))
            FoodsToAvoid.Add(result);
    }

    [RelayCommand]
    private void RemoveFoodToAvoid(string food) => FoodsToAvoid.Remove(food);

    [RelayCommand]
    private void IncrementMeals()
    {
        if (MealsPerDay < 6) MealsPerDay++;
    }

    [RelayCommand]
    private void DecrementMeals()
    {
        if (MealsPerDay > 1) MealsPerDay--;
    }

    [RelayCommand]
    private void ToggleProteinShakes()
    {
        UsesProteinShakes = !UsesProteinShakes;
    }

    [RelayCommand]
    private void IncrementShakes()
    {
        if (ShakesPerDay < 5) ShakesPerDay++;
    }

    [RelayCommand]
    private void DecrementShakes()
    {
        if (ShakesPerDay > 1) ShakesPerDay--;
    }

    [RelayCommand]
    private void IncrementProteinPerShake()
    {
        if (ProteinPerShakeG < 60) ProteinPerShakeG += 5;
    }

    [RelayCommand]
    private void DecrementProteinPerShake()
    {
        if (ProteinPerShakeG > 10) ProteinPerShakeG -= 5;
    }

    [RelayCommand]
    private void IncrementWater()
    {
        if (DailyWaterGlasses < 16) DailyWaterGlasses++;
    }

    [RelayCommand]
    private void DecrementWater()
    {
        if (DailyWaterGlasses > 4) DailyWaterGlasses--;
    }

    [RelayCommand]
    private void IncrementMealPrepDays()
    {
        if (MealPrepDays < 7) MealPrepDays++;
    }

    [RelayCommand]
    private void DecrementMealPrepDays()
    {
        if (MealPrepDays > 2) MealPrepDays--;
    }

    public void SyncToCoordinator()
    {
        _coordinator.Data.SelectedDietType = SelectedDietType;
        _coordinator.Data.SelectedAllergies = [.. SelectedAllergies];
        _coordinator.Data.FoodsToAvoid = [.. FoodsToAvoid];
        _coordinator.Data.MealsPerDay = MealsPerDay;
        _coordinator.Data.UsesProteinShakes = UsesProteinShakes;
        _coordinator.Data.ShakesPerDay = ShakesPerDay;
        _coordinator.Data.ProteinPerShakeG = ProteinPerShakeG;
        _coordinator.Data.DailyWaterGlasses = DailyWaterGlasses;
        _coordinator.Data.MealPrepMode = MealPrepMode;
        _coordinator.Data.MealPrepDays = MealPrepDays;
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        SyncToCoordinator();
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        SyncToCoordinator();
        await _coordinator.GoPreviousAsync();
    }
}

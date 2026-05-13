using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

[QueryProperty(nameof(BarcodeParam), "barcode")]
public partial class AddCustomFoodViewModel : BaseViewModel
{
    private readonly IFoodService _foodService;

    public AddCustomFoodViewModel(IFoodService foodService)
    {
        _foodService = foodService;
        Title = "Add Custom Food";
    }

    [ObservableProperty]
    private string _barcodeParam = string.Empty;

    [ObservableProperty]
    private string _foodName = string.Empty;

    [ObservableProperty]
    private string _brand = string.Empty;

    [ObservableProperty]
    private FoodCategory _selectedCategory = FoodCategory.Protein;

    [ObservableProperty]
    private string _barcode = string.Empty;

    [ObservableProperty]
    private string _caloriesPer100g = string.Empty;

    [ObservableProperty]
    private string _proteinPer100g = string.Empty;

    [ObservableProperty]
    private string _carbsPer100g = string.Empty;

    [ObservableProperty]
    private string _fatPer100g = string.Empty;

    [ObservableProperty]
    private string _fiberPer100g = string.Empty;

    [ObservableProperty]
    private string _defaultServingSize = "100";

    [ObservableProperty]
    private string _defaultServingLabel = "100g";

    [ObservableProperty]
    private string _nameError = string.Empty;

    [ObservableProperty]
    private string _caloriesError = string.Empty;

    [ObservableProperty]
    private bool _hasNameError;

    [ObservableProperty]
    private bool _hasCaloriesError;

    public List<FoodCategory> FoodCategories { get; } = Enum.GetValues<FoodCategory>().ToList();

    partial void OnBarcodeParamChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Barcode = value;
        }
    }

    [RelayCommand]
    private async Task SaveFoodAsync()
    {
        if (!ValidateForm()) return;

        IsBusy = true;
        try
        {
            var food = new Food
            {
                Name = FoodName.Trim(),
                Brand = string.IsNullOrWhiteSpace(Brand) ? null : Brand.Trim(),
                FoodCategory = SelectedCategory,
                Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                CaloriesPer100g = ParseDouble(CaloriesPer100g),
                ProteinPer100g = ParseDouble(ProteinPer100g),
                CarbsPer100g = ParseDouble(CarbsPer100g),
                FatPer100g = ParseDouble(FatPer100g),
                FiberPer100g = ParseDouble(FiberPer100g),
                DefaultServingSize = ParseDouble(DefaultServingSize, 100),
                DefaultServingLabel = string.IsNullOrWhiteSpace(DefaultServingLabel)
                    ? "100g"
                    : DefaultServingLabel.Trim(),
                IsUserCreated = true,
                IsActive = true
            };

            await _foodService.AddCustomFoodAsync(food);

            await Shell.Current.DisplayAlert("Success", $"{food.Name} has been added to your food database!", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to save food: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ValidateForm()
    {
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(FoodName))
        {
            NameError = "Food name is required";
            HasNameError = true;
            isValid = false;
        }
        else
        {
            NameError = string.Empty;
            HasNameError = false;
        }

        if (string.IsNullOrWhiteSpace(CaloriesPer100g) || !double.TryParse(CaloriesPer100g, out var cal) || cal < 0)
        {
            CaloriesError = "Please enter a valid calorie value";
            HasCaloriesError = true;
            isValid = false;
        }
        else
        {
            CaloriesError = string.Empty;
            HasCaloriesError = false;
        }

        return isValid;
    }

    private static double ParseDouble(string value, double defaultValue = 0)
    {
        return double.TryParse(value, out var result) ? result : defaultValue;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

[QueryProperty(nameof(MealTypeParam), "mealType")]
public partial class AddFoodEntryViewModel : BaseViewModel
{
    private readonly IFoodService _foodService;
    private readonly IUserService _userService;

    public AddFoodEntryViewModel(IFoodService foodService, IUserService userService)
    {
        _foodService = foodService;
        _userService = userService;
        Title = "Add Food";
    }

    [ObservableProperty]
    private string _mealTypeParam = string.Empty;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FoodSearchResult> _searchResults = new();

    [ObservableProperty]
    private bool _hasSearchResults;

    [ObservableProperty]
    private bool _hasSearched;

    [ObservableProperty]
    private FoodSearchResult? _selectedFood;

    [ObservableProperty]
    private bool _isFoodSelected;

    [ObservableProperty]
    private double _servingSize = 100;

    [ObservableProperty]
    private string _servingSizeText = "100";

    [ObservableProperty]
    private MealType _selectedMealType = MealType.Breakfast;

    [ObservableProperty]
    private string _estimatedCalories = "0 kcal";

    [ObservableProperty]
    private string _estimatedProtein = "0g";

    [ObservableProperty]
    private string _estimatedCarbs = "0g";

    [ObservableProperty]
    private string _estimatedFat = "0g";

    public List<MealType> MealTypes { get; } = Enum.GetValues<MealType>().ToList();

    private CancellationTokenSource? _searchCts;

    partial void OnSearchQueryChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                    MainThread.BeginInvokeOnMainThread(() => SearchFoodsCommand.Execute(null));
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    partial void OnMealTypeParamChanged(string value)
    {
        if (Enum.TryParse<MealType>(value, out var mealType))
        {
            SelectedMealType = mealType;
        }
    }

    partial void OnServingSizeChanged(double value)
    {
        ServingSizeText = value.ToString("F0");
        UpdateEstimates();
    }

    [RelayCommand]
    private async Task SearchFoodsAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            HasSearched = false;
            HasSearchResults = false;
            SearchResults = new ObservableCollection<FoodSearchResult>();
            return;
        }

        IsBusy = true;
        HasSearched = true;
        try
        {
            var results = await _foodService.SearchFoodsAsync(SearchQuery);
            var displayResults = results.Select(f => new FoodSearchResult
            {
                FoodId = f.Id,
                Name = f.Name,
                Brand = f.Brand ?? string.Empty,
                CaloriesPer100g = f.CaloriesPer100g,
                ProteinPer100g = f.ProteinPer100g,
                CarbsPer100g = f.CarbsPer100g,
                FatPer100g = f.FatPer100g,
                DefaultServingSize = f.DefaultServingSize,
                DefaultServingLabel = f.DefaultServingLabel,
                CaloriesDisplay = $"{f.CaloriesPer100g:F0} kcal/100g",
                DisplayName = string.IsNullOrEmpty(f.Brand) ? f.Name : $"{f.Name} ({f.Brand})"
            }).ToList();

            SearchResults = new ObservableCollection<FoodSearchResult>(displayResults);
            HasSearchResults = displayResults.Count > 0;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Food search", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectFood(FoodSearchResult food)
    {
        if (food == null) return;

        SelectedFood = food;
        IsFoodSelected = true;
        ServingSize = food.DefaultServingSize;
        UpdateEstimates();
    }

    private void UpdateEstimates()
    {
        if (SelectedFood == null) return;

        double factor = ServingSize / 100.0;
        double cal = SelectedFood.CaloriesPer100g * factor;
        double pro = SelectedFood.ProteinPer100g * factor;
        double carb = SelectedFood.CarbsPer100g * factor;
        double fat = SelectedFood.FatPer100g * factor;

        EstimatedCalories = $"{cal:F0} kcal";
        EstimatedProtein = $"{pro:F1}g";
        EstimatedCarbs = $"{carb:F1}g";
        EstimatedFat = $"{fat:F1}g";
    }

    [RelayCommand]
    private async Task LogFoodAsync()
    {
        if (SelectedFood == null) return;

        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            await _foodService.LogFoodAsync(user.Id, SelectedFood.FoodId, SelectedMealType, ServingSize, DateTime.Today);

            await Shell.Current.DisplayAlert("Success", $"{SelectedFood.Name} logged successfully!", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to log food: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ScanBarcodeAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.BarcodeScanner);
    }

    [RelayCommand]
    private async Task AddCustomFoodAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.AddCustomFood);
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedFood = null;
        IsFoodSelected = false;
        ServingSize = 100;
        EstimatedCalories = "0 kcal";
        EstimatedProtein = "0g";
        EstimatedCarbs = "0g";
        EstimatedFat = "0g";
    }
}

public class FoodSearchResult
{
    public int FoodId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double CaloriesPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public double CarbsPer100g { get; set; }
    public double FatPer100g { get; set; }
    public double DefaultServingSize { get; set; } = 100;
    public string DefaultServingLabel { get; set; } = "100g";
    public string CaloriesDisplay { get; set; } = string.Empty;
}

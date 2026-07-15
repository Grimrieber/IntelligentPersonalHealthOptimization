using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

[QueryProperty(nameof(MealTypeParam), "mealType")]
[QueryProperty(nameof(FoodIdParam), "foodId")]
public partial class AddFoodEntryViewModel : BaseViewModel
{
    private readonly IFoodService _foodService;
    private readonly IUserService _userService;
    private readonly INutritionService _nutritionService;

    public AddFoodEntryViewModel(IFoodService foodService, IUserService userService,
        INutritionService nutritionService)
    {
        _foodService = foodService;
        _userService = userService;
        _nutritionService = nutritionService;
        Title = "Add Food";
    }

    [ObservableProperty]
    private string _mealTypeParam = string.Empty;

    // Set by the barcode scanner (RecipeDetail?foodId=…) — pre-selects the scanned
    // food so the user lands ready to log instead of on an empty search page.
    [ObservableProperty]
    private string _foodIdParam = string.Empty;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FoodSearchResult> _searchResults = new();

    [ObservableProperty]
    private bool _hasSearchResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecentFoods))]
    private bool _hasSearched;

    [ObservableProperty]
    private FoodSearchResult? _selectedFood;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecentFoods))]
    private bool _isFoodSelected;

    // Recently-logged foods for one-tap re-add (shown only when idle: no active
    // search text and nothing selected yet).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecentFoods))]
    private ObservableCollection<FoodSearchResult> _recentFoods = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecentFoods))]
    private bool _hasRecentFoods;

    public bool ShowRecentFoods => HasRecentFoods && !HasSearched && !IsFoodSelected;

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

    // Goal context — how this food lands against today's remaining calories.
    [ObservableProperty]
    private bool _hasGoalContext;

    [ObservableProperty]
    private string _remainingBeforeText = string.Empty;

    [ObservableProperty]
    private string _afterThisText = string.Empty;

    [ObservableProperty]
    private bool _isAfterOver;

    private double _targetCalories;
    private double _consumedCalories;

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

    partial void OnFoodIdParamChanged(string value)
    {
        if (int.TryParse(value, out var id) && id > 0)
            _ = PreselectFoodAsync(id);
    }

    private async Task PreselectFoodAsync(int foodId)
    {
        try
        {
            var f = await _foodService.GetFoodByIdAsync(foodId);
            if (f == null) return;

            var result = new FoodSearchResult
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
            };
            MainThread.BeginInvokeOnMainThread(() => SelectFood(result));
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Preselect scanned food", ex);
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
            var displayResults = results.Select(ToResult).ToList();

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

    private static FoodSearchResult ToResult(Food f) => new()
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
    };

    /// <summary>Load the user's recently-logged foods for the one-tap re-add list.</summary>
    private async Task LoadRecentFoodsAsync()
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;
            var recent = await _foodService.GetRecentlyLoggedFoodsAsync(user.Id, 8);
            RecentFoods = new ObservableCollection<FoodSearchResult>(recent.Select(ToResult));
            HasRecentFoods = RecentFoods.Count > 0;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Load recent foods", ex);
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

        UpdateRemaining();
    }

    [RelayCommand]
    private async Task LoadContextAsync()
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            // Recent-foods list is independent of goal context — load it regardless.
            await LoadRecentFoodsAsync();

            var profile = await _nutritionService.GetNutritionProfileAsync(user.Id);
            if (profile == null || profile.TargetCalories <= 0)
            {
                HasGoalContext = false;
                return;
            }

            var totals = await _foodService.GetDailyTotalsAsync(user.Id, DateTime.Today);
            _targetCalories = profile.TargetCalories;
            _consumedCalories = totals.calories;
            HasGoalContext = true;
            UpdateRemaining();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("AddFood goal context", ex);
        }
    }

    private void UpdateRemaining()
    {
        if (!HasGoalContext) return;

        var before = _targetCalories - _consumedCalories;
        RemainingBeforeText = before >= 0
            ? $"{before:F0} kcal left today"
            : $"{Math.Abs(before):F0} kcal over today";

        if (SelectedFood == null)
        {
            AfterThisText = string.Empty;
            return;
        }

        var est = SelectedFood.CaloriesPer100g * (ServingSize / 100.0);
        var after = before - est;
        IsAfterOver = after < 0;
        AfterThisText = after >= 0
            ? $"→ {after:F0} kcal left after this"
            : $"→ {Math.Abs(after):F0} kcal over after this";
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
        UpdateRemaining();
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

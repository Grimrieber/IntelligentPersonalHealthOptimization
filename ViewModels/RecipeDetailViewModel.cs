using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Data;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;

namespace IntelligentPersonalHealthOptimization.ViewModels;

[QueryProperty(nameof(RecipeId), "recipeId")]
[QueryProperty(nameof(SavedRecipeId), "savedRecipeId")]
public partial class RecipeDetailViewModel : BaseViewModel
{
    private readonly IRecipeService _recipeService;
    private readonly ISavedRecipeService _savedRecipeService;
    private readonly IUserService _userService;
    private readonly INutritionService _nutritionService;
    private readonly IDatabaseService _databaseService;

    public RecipeDetailViewModel(
        IRecipeService recipeService,
        ISavedRecipeService savedRecipeService,
        IUserService userService,
        INutritionService nutritionService,
        IDatabaseService databaseService)
    {
        _recipeService = recipeService;
        _savedRecipeService = savedRecipeService;
        _userService = userService;
        _nutritionService = nutritionService;
        _databaseService = databaseService;
    }

    /// <summary>Open the distraction-free step-by-step cooking view.</summary>
    [RelayCommand]
    private async Task StartCookingAsync()
    {
        var id = RecipeId > 0 ? RecipeId : SavedRecipeId;
        if (id > 0)
            await Shell.Current.GoToAsync($"{Constants.RouteConstants.CookMode}?recipeId={id}");
    }

    /// <summary>Log this recipe to today's food log (respecting the serving scaler),
    /// after asking which meal it counts as.</summary>
    [RelayCommand]
    private async Task LogRecipeAsync()
    {
        if (!_baseCal.HasValue)
        {
            await Shell.Current.DisplayAlert("Can't log", "This recipe has no nutrition to log.", "OK");
            return;
        }

        var choice = await Shell.Current.DisplayActionSheet(
            "Log to today as…", "Cancel", null, "Breakfast", "Lunch", "Dinner", "Snack");
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var mealType = choice switch
        {
            "Breakfast" => Models.Enums.MealType.Breakfast,
            "Lunch" => Models.Enums.MealType.Lunch,
            "Dinner" => Models.Enums.MealType.Dinner,
            _ => Models.Enums.MealType.AfternoonSnack,
        };

        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var m = ServingMultiplier;
            var entry = new Models.FoodLogEntry
            {
                UserId = user.Id,
                LogDate = DateTime.Today,
                MealType = mealType,
                SavedRecipeId = RecipeId > 0 ? RecipeId : SavedRecipeId,
                Calories = (_baseCal ?? 0) * m,
                ProteinG = (_baseProtein ?? 0) * m,
                CarbsG = (_baseCarbs ?? 0) * m,
                FatG = (_baseFat ?? 0) * m,
                Notes = RecipeName,
            };
            await _databaseService.InsertAsync(entry);

            var kcal = (_baseCal ?? 0) * m;
            await Shell.Current.DisplayAlert("Logged",
                $"{RecipeName} ({m} serving{(m > 1 ? "s" : "")}, {kcal:F0} kcal) added to today.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not log recipe: {ex.Message}", "OK");
        }
    }

    [ObservableProperty]
    private int _recipeId = -1;

    [ObservableProperty]
    private int _savedRecipeId = -1;

    [ObservableProperty]
    private RecipeDetail? _recipe;

    [ObservableProperty]
    private bool _isSaved;

    [ObservableProperty]
    private string _saveButtonText = "Save Recipe";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? _imageUrl;

    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);

    // Formatted display properties
    [ObservableProperty]
    private string _recipeName = string.Empty;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private string _prepTime = string.Empty;

    [ObservableProperty]
    private string _cookTime = string.Empty;

    [ObservableProperty]
    private string _restTime = string.Empty;

    [ObservableProperty]
    private string _servings = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _hasNotes;

    [ObservableProperty]
    private bool _hasNutrition;

    // Macro split bar (protein/carbs/fat by calorie contribution).
    [ObservableProperty]
    private bool _hasMacroBar;

    [ObservableProperty]
    private GridLength _proteinStar = new(1, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _carbsStar = new(1, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _fatStar = new(1, GridUnitType.Star);

    [ObservableProperty]
    private string _proteinPct = string.Empty;

    [ObservableProperty]
    private string _carbsPct = string.Empty;

    [ObservableProperty]
    private string _fatPct = string.Empty;

    /// <summary>Compute the macro-calorie split (P·4, C·4, F·9) for the split bar.</summary>
    private void SetMacroBar(double? protein, double? carbs, double? fat)
    {
        var p = Math.Max(0, protein ?? 0) * 4.0;
        var c = Math.Max(0, carbs ?? 0) * 4.0;
        var f = Math.Max(0, fat ?? 0) * 9.0;
        var total = p + c + f;
        if (total <= 0) { HasMacroBar = false; return; }
        ProteinStar = new GridLength(Math.Max(p, 0.001), GridUnitType.Star);
        CarbsStar = new GridLength(Math.Max(c, 0.001), GridUnitType.Star);
        FatStar = new GridLength(Math.Max(f, 0.001), GridUnitType.Star);
        ProteinPct = $"P {Math.Round(100 * p / total)}%";
        CarbsPct = $"C {Math.Round(100 * c / total)}%";
        FatPct = $"F {Math.Round(100 * f / total)}%";
        HasMacroBar = true;
    }

    // ---- "Fits your goals" ----
    [ObservableProperty]
    private bool _showGoalFit;

    [ObservableProperty]
    private string _goalFitVerdict = string.Empty;

    [ObservableProperty]
    private string _goalFitDetail = string.Empty;

    [ObservableProperty]
    private Color _goalFitColor = Colors.Gray;

    /// <summary>Fetch the user's targets and rate how this recipe fits their goal.</summary>
    private async Task ApplyGoalFitAsync(int? calories, double? protein, string? tier)
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            var profile = user != null ? await _nutritionService.GetNutritionProfileAsync(user.Id) : null;
            SetGoalFit(calories, protein, tier, profile);
        }
        catch
        {
            ShowGoalFit = false;
        }
    }

    private void SetGoalFit(int? calories, double? protein, string? tier, Models.NutritionProfile? profile)
    {
        if (profile == null || profile.TargetCalories <= 0 || calories is not int cal || cal <= 0)
        {
            ShowGoalFit = false;
            return;
        }

        var calShare = (int)Math.Round(100.0 * cal / profile.TargetCalories);
        // Protein density (g per 100 kcal) — the fitness-relevant lens.
        double? ppk = protein is double pr && pr > 0 ? pr * 100.0 / cal : null;

        string verdict;
        Color color;
        if (tier == RecipeHealth.Healthy && ppk is >= 8)
        {
            verdict = "Great fit for your goals";
            color = HealthBadgeStyle.ColorFor(RecipeHealth.Healthy);
        }
        else if (tier == RecipeHealth.Indulgent)
        {
            verdict = "Better as an occasional treat";
            color = HealthBadgeStyle.ColorFor(RecipeHealth.Indulgent);
        }
        else if (ppk is < 5 && cal >= 500)
        {
            verdict = "Rich — mind the portion";
            color = HealthBadgeStyle.ColorFor(RecipeHealth.Moderate);
        }
        else
        {
            verdict = "Fine in moderation";
            color = HealthBadgeStyle.ColorFor(RecipeHealth.Moderate);
        }

        var detail = $"≈{calShare}% of your {profile.TargetCalories:N0} kcal day";
        if (ppk is double d)
            detail += $"  ·  {Math.Round(d)}g protein per 100 kcal";

        GoalFitVerdict = verdict;
        GoalFitColor = color;
        GoalFitDetail = detail;
        ShowGoalFit = true;
    }

    // Health-consciousness tier badge (see RecipeHealth).
    [ObservableProperty]
    private bool _showHealthBadge;

    [ObservableProperty]
    private string _healthBadgeText = string.Empty;

    [ObservableProperty]
    private Color _healthBadgeColor = Colors.Gray;

    [ObservableProperty]
    private bool _hasPrepTime;

    [ObservableProperty]
    private bool _hasCookTime;

    [ObservableProperty]
    private bool _hasRestTime;

    // Nutrition
    [ObservableProperty]
    private string _calories = string.Empty;

    [ObservableProperty]
    private string _protein = string.Empty;

    [ObservableProperty]
    private string _carbs = string.Empty;

    [ObservableProperty]
    private string _fat = string.Empty;

    [ObservableProperty]
    private string _fiber = string.Empty;

    [ObservableProperty]
    private string _sugar = string.Empty;

    [ObservableProperty]
    private string _saturatedFat = string.Empty;

    [ObservableProperty]
    private string _cholesterol = string.Empty;

    [ObservableProperty]
    private string _sodium = string.Empty;

    [ObservableProperty]
    private string _servingSizeNote = string.Empty;

    // ---- serving scaler ----
    // Base (per-serving) nutrition captured on load; the displayed values are these
    // scaled by ServingMultiplier so the user can portion to their macros.
    private int? _baseCal;
    private double? _baseProtein, _baseCarbs, _baseFat, _baseFiber, _baseSugar, _baseSatFat, _baseSodium, _baseChol;

    [ObservableProperty]
    private int _servingMultiplier = 1;

    public bool CanDecreaseServings => ServingMultiplier > 1;
    public string ScaleLabel => ServingMultiplier == 1 ? "Per serving" : $"For {ServingMultiplier} servings";

    [RelayCommand]
    private void IncreaseServings()
    {
        if (ServingMultiplier >= 12) return;
        ServingMultiplier++;
        ApplyServingScale();
    }

    [RelayCommand]
    private void DecreaseServings()
    {
        if (ServingMultiplier <= 1) return;
        ServingMultiplier--;
        ApplyServingScale();
    }

    private void SetBaseNutrition(int? cal, double? protein, double? carbs, double? fat,
        double? fiber, double? sugar, double? satfat, double? sodium, double? chol)
    {
        _baseCal = cal; _baseProtein = protein; _baseCarbs = carbs; _baseFat = fat;
        _baseFiber = fiber; _baseSugar = sugar; _baseSatFat = satfat; _baseSodium = sodium; _baseChol = chol;
        ServingMultiplier = 1;
        ApplyServingScale();
    }

    private void ApplyServingScale()
    {
        var m = ServingMultiplier;
        Calories = _baseCal.HasValue ? (_baseCal.Value * m).ToString() : "--";
        Protein = _baseProtein.HasValue ? $"{_baseProtein.Value * m:F1}g" : "--";
        Carbs = _baseCarbs.HasValue ? $"{_baseCarbs.Value * m:F1}g" : "--";
        Fat = _baseFat.HasValue ? $"{_baseFat.Value * m:F1}g" : "--";
        Fiber = _baseFiber.HasValue ? $"{_baseFiber.Value * m:F1}g" : "--";
        Sugar = _baseSugar.HasValue ? $"{_baseSugar.Value * m:F1}g" : "--";
        SaturatedFat = _baseSatFat.HasValue ? $"{_baseSatFat.Value * m:F1}g" : "--";
        Sodium = _baseSodium.HasValue ? $"{_baseSodium.Value * m:F1}mg" : "--";
        Cholesterol = _baseChol.HasValue ? $"{_baseChol.Value * m:F1}mg" : "--";
        OnPropertyChanged(nameof(ScaleLabel));
        OnPropertyChanged(nameof(CanDecreaseServings));
    }

    // Grouped data for display
    [ObservableProperty]
    private ObservableCollection<IngredientGrouping> _ingredientGroups = [];

    [ObservableProperty]
    private ObservableCollection<DirectionGrouping> _directionGroups = [];

    partial void OnRecipeIdChanged(int value)
    {
        if (value >= 0 && SavedRecipeId < 0)
            _ = LoadRecipeAsync();
    }

    partial void OnSavedRecipeIdChanged(int value)
    {
        if (value > 0)
            _ = LoadSavedRecipeAsync();
    }

    [RelayCommand]
    private async Task LoadRecipeAsync()
    {
        if (IsBusy || RecipeId < 0) return;
        IsBusy = true;

        try
        {
            Recipe = await _recipeService.GetRecipeDetailAsync(RecipeId);

            if (Recipe == null)
            {
                await Shell.Current.DisplayAlert("Error", "Recipe not found.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Populate display properties
            RecipeName = Recipe.Recipe.RecipeName;
            CategoryName = Recipe.Recipe.CategoryName;
            Title = RecipeName;

            PrepTime = Recipe.Recipe.PrepTime ?? string.Empty;
            CookTime = Recipe.Recipe.CookTime ?? string.Empty;
            RestTime = Recipe.Recipe.RestTime ?? string.Empty;
            Servings = Recipe.Recipe.Servings ?? string.Empty;
            Notes = Recipe.Recipe.Notes ?? string.Empty;

            HasPrepTime = !string.IsNullOrEmpty(PrepTime);
            HasCookTime = !string.IsNullOrEmpty(CookTime);
            HasRestTime = !string.IsNullOrEmpty(RestTime);
            HasNotes = !string.IsNullOrEmpty(Notes);

            // Nutrition
            HasNutrition = Recipe.Nutrition != null;
            if (Recipe.Nutrition != null)
            {
                var n = Recipe.Nutrition;
                SetBaseNutrition(n.CaloriesPerServing, (double?)n.ProteinGrams, (double?)n.TotalCarbsGrams,
                    (double?)n.TotalFatGrams, (double?)n.FiberGrams, (double?)n.SugarGrams,
                    (double?)n.SaturatedFatGrams, (double?)n.SodiumMg, (double?)n.CholesterolMg);
                ServingSizeNote = n.ServingSizeNote ?? string.Empty;
                SetMacroBar((double?)n.ProteinGrams, (double?)n.TotalCarbsGrams, (double?)n.TotalFatGrams);
                await ApplyGoalFitAsync(n.CaloriesPerServing, (double?)n.ProteinGrams, Recipe.Recipe?.HealthTier);
            }

            // Grouped ingredients and directions
            IngredientGroups = new ObservableCollection<IngredientGrouping>(Recipe.GroupedIngredients);
            DirectionGroups = new ObservableCollection<DirectionGrouping>(Recipe.GroupedDirections);

            // Cookbook recipes are favorited in place: RecipeId is the catalog
            // row's Id, so "saved" means IsFavorite is set on that row.
            ImageUrl = Recipe.Recipe?.ImageUrl;
            SetHealthBadge(Recipe.Recipe?.HealthTier, Recipe.Recipe?.IsHealthyTreat ?? false);
            IsSaved = await _savedRecipeService.IsFavoriteAsync(RecipeId);
            UpdateSaveButton();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSavedRecipeAsync()
    {
        if (IsBusy || SavedRecipeId <= 0) return;
        IsBusy = true;

        try
        {
            var saved = await _savedRecipeService.GetSavedRecipeAsync(SavedRecipeId);
            if (saved == null)
            {
                await Shell.Current.DisplayAlert("Error", "Recipe not found.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            RecipeName = saved.RecipeName;
            CategoryName = saved.CategoryName;
            ImageUrl = saved.ImageUrl;
            Title = RecipeName;

            PrepTime = saved.PrepTime ?? string.Empty;
            CookTime = saved.CookTime ?? string.Empty;
            RestTime = saved.RestTime ?? string.Empty;
            Servings = saved.Servings ?? string.Empty;
            Notes = saved.Notes ?? string.Empty;

            HasPrepTime = !string.IsNullOrEmpty(PrepTime);
            HasCookTime = !string.IsNullOrEmpty(CookTime);
            HasRestTime = !string.IsNullOrEmpty(RestTime);
            HasNotes = !string.IsNullOrEmpty(Notes);

            // Nutrition from saved snapshot
            HasNutrition = saved.CaloriesPerServing.HasValue;
            SetBaseNutrition(saved.CaloriesPerServing, saved.ProteinGrams, saved.CarbsGrams,
                saved.FatGrams, saved.FiberGrams, saved.SugarGrams, saved.SatFatGrams,
                saved.SodiumMg, saved.CholesterolMg);
            ServingSizeNote = saved.ServingSizeNote ?? string.Empty;
            SetMacroBar(saved.ProteinGrams, saved.CarbsGrams, saved.FatGrams);
            await ApplyGoalFitAsync(saved.CaloriesPerServing, saved.ProteinGrams, saved.HealthTier);

            // Load ingredients and directions from local DB
            var ingredients = await _savedRecipeService.GetSavedIngredientsAsync(SavedRecipeId);
            var directions = await _savedRecipeService.GetSavedDirectionsAsync(SavedRecipeId);

            // Group ingredients
            var ingredientGrouped = ingredients
                .GroupBy(i => i.IngredientGroup ?? string.Empty)
                .Select(g => new IngredientGrouping(
                    g.Key,
                    g.Select(i => new RecipeIngredient
                    {
                        IngredientID = i.Id,
                        RecipeID = 0,
                        SortOrder = i.SortOrder,
                        IngredientGroup = i.IngredientGroup,
                        Description = i.Description
                    }).ToList()
                ));
            IngredientGroups = new ObservableCollection<IngredientGrouping>(ingredientGrouped);

            // Group directions
            var directionGrouped = directions
                .GroupBy(d => d.DirectionGroup ?? string.Empty)
                .Select(g => new DirectionGrouping(
                    g.Key,
                    g.Select(d => new RecipeDirection
                    {
                        DirectionID = d.Id,
                        RecipeID = 0,
                        StepNumber = d.StepNumber,
                        DirectionGroup = d.DirectionGroup,
                        Instruction = d.Instruction
                    }).ToList()
                ));
            DirectionGroups = new ObservableCollection<DirectionGrouping>(directionGrouped);

            SetHealthBadge(saved.HealthTier, saved.IsHealthyTreat);

            // Mark as saved
            IsSaved = true;
            UpdateSaveButton();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleSaveAsync()
    {
        // The catalog lives in the SavedRecipe table, so the favourite id is
        // RecipeId on the cookbook path and SavedRecipeId when opened from a
        // meal plan. (Previously this guarded on Recipe != null, which is only
        // set on the cookbook path — so the star silently no-op'd from a plan.)
        var favoriteId = RecipeId > 0 ? RecipeId : SavedRecipeId;
        if (favoriteId <= 0) return;

        try
        {
            // Flip IsFavorite on the catalog row — same mechanism the star on
            // the browse list uses, so the two stay in sync.
            await _savedRecipeService.ToggleFavoriteAsync(favoriteId);
            IsSaved = !IsSaved;
            UpdateSaveButton();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not save recipe: {ex.Message}", "OK");
        }
    }

    private void UpdateSaveButton()
    {
        SaveButtonText = IsSaved ? "Remove from Saved" : "Save Recipe";
    }

    /// <summary>Set the health-tier badge from a classified row.</summary>
    private void SetHealthBadge(string? tier, bool isTreat)
    {
        ShowHealthBadge = !string.IsNullOrEmpty(tier);
        HealthBadgeText = isTreat ? "Healthy Treat" : (tier ?? string.Empty);
        HealthBadgeColor = tier switch
        {
            RecipeHealth.Healthy => Color.FromArgb("#1F8A4C"),
            RecipeHealth.Moderate => Color.FromArgb("#B9791A"),
            RecipeHealth.Indulgent => Color.FromArgb("#BB5340"),
            _ => Colors.Gray,
        };
    }
}

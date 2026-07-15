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
    private readonly IFoodService _foodService;

    public RecipeDetailViewModel(
        IRecipeService recipeService,
        ISavedRecipeService savedRecipeService,
        IUserService userService,
        INutritionService nutritionService,
        IDatabaseService databaseService,
        IFoodService foodService)
    {
        _recipeService = recipeService;
        _savedRecipeService = savedRecipeService;
        _userService = userService;
        _nutritionService = nutritionService;
        _databaseService = databaseService;
        _foodService = foodService;
    }

    /// <summary>Add every ingredient of this recipe to the shopping list.</summary>
    [RelayCommand]
    private async Task AddToShoppingListAsync()
    {
        var descriptions = IngredientGroups
            .SelectMany(g => g.Items)
            .Select(i => i.Description)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToList();

        if (descriptions.Count == 0)
        {
            await Shell.Current.DisplayAlert("Nothing to add", "This recipe has no ingredients listed.", "OK");
            return;
        }

        Data.ExtraShoppingItems.Add(RecipeName, descriptions);
        await Shell.Current.DisplayAlert("Added to Shopping List",
            $"{descriptions.Count} ingredient{(descriptions.Count == 1 ? "" : "s")} from {RecipeName} added to your shopping list.", "OK");
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

    // "Fits your remaining macros today" — only meaningful once something's logged today.
    [ObservableProperty]
    private bool _showRemainingToday;

    [ObservableProperty]
    private string _remainingTodayText = string.Empty;

    [ObservableProperty]
    private Color _remainingTodayColor = Colors.Gray;

    /// <summary>Fetch the user's targets and rate how this recipe fits their goal,
    /// plus how it fits what's left of today's calorie/protein budget.</summary>
    private async Task ApplyGoalFitAsync(int? calories, double? protein, string? tier)
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            var profile = user != null ? await _nutritionService.GetNutritionProfileAsync(user.Id) : null;
            SetGoalFit(calories, protein, tier, profile);

            ShowRemainingToday = false;
            if (user != null && profile != null && profile.TargetCalories > 0 && calories is int cal && cal > 0)
            {
                var totals = await _foodService.GetDailyTotalsAsync(user.Id, DateTime.Today);
                if (totals.calories > 0) // only once they've logged today
                {
                    var remCal = Math.Max(0, profile.TargetCalories - totals.calories);
                    var remPro = Math.Max(0, profile.TargetProteinG - totals.proteinG);
                    var fits = cal <= remCal;
                    RemainingTodayColor = fits
                        ? HealthBadgeStyle.ColorFor(RecipeHealth.Healthy)
                        : HealthBadgeStyle.ColorFor(RecipeHealth.Indulgent);
                    RemainingTodayText = fits
                        ? $"Fits today — you have {remCal:F0} kcal · {remPro:F0}g protein left"
                        : $"Over budget — only {remCal:F0} kcal left today (this is {cal})";
                    ShowRemainingToday = true;
                }
            }
        }
        catch
        {
            ShowGoalFit = false;
            ShowRemainingToday = false;
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

    // ---- Personal rating & notes ----
    // Persisted on the recipe's SavedRecipe row (MyRating / MyNote). The star row
    // is five tappable glyphs whose colour is driven by Star1..5Color; MyNoteText
    // is the two-way-bound editor text, saved explicitly.
    private static readonly Color StarOn = Color.FromArgb("#F2B01E");
    private static readonly Color StarOff = Color.FromArgb("#4A4A4A");

    [ObservableProperty]
    private int _myRating;

    [ObservableProperty]
    private string _myNoteText = string.Empty;

    [ObservableProperty]
    private string _ratingSummary = "Tap to rate";

    [ObservableProperty]
    private Color _star1Color = StarOff;
    [ObservableProperty]
    private Color _star2Color = StarOff;
    [ObservableProperty]
    private Color _star3Color = StarOff;
    [ObservableProperty]
    private Color _star4Color = StarOff;
    [ObservableProperty]
    private Color _star5Color = StarOff;

    private int TargetSavedId => RecipeId > 0 ? RecipeId : SavedRecipeId;

    private void PaintStars(int rating)
    {
        Star1Color = rating >= 1 ? StarOn : StarOff;
        Star2Color = rating >= 2 ? StarOn : StarOff;
        Star3Color = rating >= 3 ? StarOn : StarOff;
        Star4Color = rating >= 4 ? StarOn : StarOff;
        Star5Color = rating >= 5 ? StarOn : StarOff;
        RatingSummary = rating <= 0
            ? "Tap to rate"
            : $"You rated this {rating}/5";
    }

    private void SetMyPersonal(int rating, string? note)
    {
        MyRating = Math.Clamp(rating, 0, 5);
        MyNoteText = note ?? string.Empty;
        PaintStars(MyRating);
    }

    /// <summary>Tap a star to set the rating (tapping the current rating clears it).</summary>
    [RelayCommand]
    private async Task SetRatingAsync(string? value)
    {
        if (!int.TryParse(value, out var stars)) return;
        if (TargetSavedId <= 0) return;

        // Tapping the same star again clears the rating.
        var newRating = stars == MyRating ? 0 : stars;
        MyRating = newRating;
        PaintStars(newRating);
        try { await _savedRecipeService.SetMyRatingAsync(TargetSavedId, newRating); }
        catch { /* non-critical */ }
    }

    [RelayCommand]
    private async Task SaveNoteAsync()
    {
        if (TargetSavedId <= 0) return;
        try
        {
            await _savedRecipeService.SetMyNoteAsync(TargetSavedId, MyNoteText);
            await Shell.Current.DisplayAlert("Saved", "Your note was saved.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not save note: {ex.Message}", "OK");
        }
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

    // Allergen warning (ingredients matching the user's allergies / foods-to-avoid).
    [ObservableProperty]
    private bool _hasAllergenWarning;

    [ObservableProperty]
    private string _allergenWarningText = string.Empty;

    /// <summary>Flag ingredient lines that hit the user's allergies/avoid-list. Call
    /// BEFORE assigning IngredientGroups so the flags are set at bind time (the
    /// RecipeIngredient flag is a plain property with no change notification).</summary>
    private async Task ApplyAllergenFlagsAsync(IEnumerable<IngredientGrouping> groups)
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            var profile = user != null ? await _nutritionService.GetNutritionProfileAsync(user.Id) : null;
            var terms = Data.AllergenMatcher.Build(profile?.Allergies, profile?.FoodsToAvoid);
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in groups)
                foreach (var ing in g.Items)
                {
                    var label = terms.Count == 0 ? null : Data.AllergenMatcher.Match(ing.Description, terms);
                    ing.AllergenFlag = label != null;
                    ing.AllergenLabel = label;
                    if (label != null) labels.Add(label);
                }
            HasAllergenWarning = labels.Count > 0;
            AllergenWarningText = labels.Count > 0
                ? "Contains foods on your avoid list: " + string.Join(", ", labels)
                : string.Empty;
        }
        catch
        {
            HasAllergenWarning = false;
        }
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
            var ingGroups = Recipe.GroupedIngredients.ToList();
            await ApplyAllergenFlagsAsync(ingGroups);
            IngredientGroups = new ObservableCollection<IngredientGrouping>(ingGroups);
            DirectionGroups = new ObservableCollection<DirectionGrouping>(Recipe.GroupedDirections);

            // Cookbook recipes are favorited in place: RecipeId is the catalog
            // row's Id, so "saved" means IsFavorite is set on that row.
            ImageUrl = Recipe.Recipe?.ImageUrl;
            SetHealthBadge(Recipe.Recipe?.HealthTier, Recipe.Recipe?.IsHealthyTreat ?? false);
            var ri = Recipe.Recipe;
            if (ri != null)
                SetDietBadges(ri.IsVegetarian, ri.IsVegan, ri.IsPescatarian, ri.IsGlutenFree,
                    ri.IsDairyFree, ri.IsKeto, ri.IsPaleo, ri.IsHalal, ri.IsKosher, ri.IsMediterranean);
            IsSaved = await _savedRecipeService.IsFavoriteAsync(RecipeId);
            UpdateSaveButton();

            // Personal rating & notes live on the catalog SavedRecipe row (Id == RecipeId).
            var savedRow = await _savedRecipeService.GetSavedRecipeAsync(RecipeId);
            SetMyPersonal(savedRow?.MyRating ?? 0, savedRow?.MyNote);

            RecordRecentView();
            await LoadSimilarRecipesAsync();
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
            var savedIngGroups = ingredientGrouped.ToList();
            await ApplyAllergenFlagsAsync(savedIngGroups);
            IngredientGroups = new ObservableCollection<IngredientGrouping>(savedIngGroups);

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
            SetDietBadges(saved.IsVegetarian, saved.IsVegan, saved.IsPescatarian, saved.IsGlutenFree,
                saved.IsDairyFree, saved.IsKeto, saved.IsPaleo, saved.IsHalal, saved.IsKosher, saved.IsMediterranean);
            SetMyPersonal(saved.MyRating, saved.MyNote);

            RecordRecentView();
            await LoadSimilarRecipesAsync();

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

    // Diet-compatibility badges (Vegan / Gluten-Free / …) from the precomputed flags.
    [ObservableProperty]
    private ObservableCollection<string> _dietBadges = [];

    [ObservableProperty]
    private bool _hasDietBadges;

    /// <summary>Build the diet-badge chips from the precomputed flags. Suppresses
    /// redundant labels (vegan implies vegetarian; vegetarian implies pescatarian).</summary>
    private void SetDietBadges(bool veg, bool vegan, bool pesc, bool gf, bool df,
        bool keto, bool paleo, bool halal, bool kosher, bool med)
    {
        var badges = new List<string>();
        if (vegan) badges.Add("Vegan");
        else if (veg) badges.Add("Vegetarian");
        else if (pesc) badges.Add("Pescatarian");
        if (gf) badges.Add("Gluten-Free");
        if (df) badges.Add("Dairy-Free");
        if (keto) badges.Add("Keto");
        if (paleo) badges.Add("Paleo");
        if (med) badges.Add("Mediterranean");
        if (halal) badges.Add("Halal");
        if (kosher) badges.Add("Kosher");

        DietBadges = new ObservableCollection<string>(badges);
        HasDietBadges = badges.Count > 0;
    }

    // Tier of the currently-loaded recipe, captured for the recently-viewed strip.
    private string? _currentTier;

    /// <summary>Record the just-opened recipe in the recently-viewed list.</summary>
    private void RecordRecentView()
    {
        Data.RecentRecipes.Add(TargetSavedId, RecipeName, ImageUrl, _currentTier, _baseCal);
    }

    // ---- "More like this" (similar recipes) ----
    [ObservableProperty]
    private ObservableCollection<Data.RecentRecipe> _similarRecipes = [];

    [ObservableProperty]
    private bool _hasSimilarRecipes;

    /// <summary>Load recipes in the same category, nearest by calories, for the
    /// "More like this" strip. Reuses the compact <see cref="Data.RecentRecipe"/>
    /// card DTO.</summary>
    private async Task LoadSimilarRecipesAsync()
    {
        HasSimilarRecipes = false;
        try
        {
            if (string.IsNullOrWhiteSpace(CategoryName)) return;
            var items = await _recipeService.GetSimilarRecipesAsync(TargetSavedId, CategoryName, _baseCal, 10);
            var cards = items
                .Select(i => new Data.RecentRecipe(i.RecipeID, i.RecipeName, i.ImageUrl, i.HealthTier, i.CaloriesPerServing))
                .ToList();
            SimilarRecipes = new ObservableCollection<Data.RecentRecipe>(cards);
            HasSimilarRecipes = cards.Count > 0;
        }
        catch
        {
            HasSimilarRecipes = false;
        }
    }

    [RelayCommand]
    private async Task OpenSimilarAsync(Data.RecentRecipe similar)
    {
        if (similar == null || similar.Id <= 0) return;
        await Shell.Current.GoToAsync($"RecipeDetail?recipeId={similar.Id}");
    }

    /// <summary>Set the health-tier badge from a classified row.</summary>
    private void SetHealthBadge(string? tier, bool isTreat)
    {
        _currentTier = tier;
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

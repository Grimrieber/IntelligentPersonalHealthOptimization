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

    public RecipeDetailViewModel(
        IRecipeService recipeService,
        ISavedRecipeService savedRecipeService,
        IUserService userService)
    {
        _recipeService = recipeService;
        _savedRecipeService = savedRecipeService;
        _userService = userService;
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
                Calories = n.CaloriesPerServing?.ToString() ?? "--";
                Protein = n.ProteinGrams.HasValue ? $"{n.ProteinGrams:F1}g" : "--";
                Carbs = n.TotalCarbsGrams.HasValue ? $"{n.TotalCarbsGrams:F1}g" : "--";
                Fat = n.TotalFatGrams.HasValue ? $"{n.TotalFatGrams:F1}g" : "--";
                Fiber = n.FiberGrams.HasValue ? $"{n.FiberGrams:F1}g" : "--";
                Sugar = n.SugarGrams.HasValue ? $"{n.SugarGrams:F1}g" : "--";
                SaturatedFat = n.SaturatedFatGrams.HasValue ? $"{n.SaturatedFatGrams:F1}g" : "--";
                Cholesterol = n.CholesterolMg.HasValue ? $"{n.CholesterolMg:F1}mg" : "--";
                Sodium = n.SodiumMg.HasValue ? $"{n.SodiumMg:F1}mg" : "--";
                ServingSizeNote = n.ServingSizeNote ?? string.Empty;
                SetMacroBar((double?)n.ProteinGrams, (double?)n.TotalCarbsGrams, (double?)n.TotalFatGrams);
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
            Calories = saved.CaloriesPerServing?.ToString() ?? "--";
            Protein = saved.ProteinGrams.HasValue ? $"{saved.ProteinGrams:F1}g" : "--";
            Carbs = saved.CarbsGrams.HasValue ? $"{saved.CarbsGrams:F1}g" : "--";
            Fat = saved.FatGrams.HasValue ? $"{saved.FatGrams:F1}g" : "--";
            Fiber = saved.FiberGrams.HasValue ? $"{saved.FiberGrams:F1}g" : "--";
            Sugar = saved.SugarGrams.HasValue ? $"{saved.SugarGrams:F1}g" : "--";
            ServingSizeNote = saved.ServingSizeNote ?? string.Empty;
            SaturatedFat = "--";
            Cholesterol = "--";
            Sodium = "--";
            SetMacroBar(saved.ProteinGrams, saved.CarbsGrams, saved.FatGrams);

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

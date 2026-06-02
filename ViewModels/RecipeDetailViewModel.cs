using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

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
            }

            // Grouped ingredients and directions
            IngredientGroups = new ObservableCollection<IngredientGrouping>(Recipe.GroupedIngredients);
            DirectionGroups = new ObservableCollection<DirectionGrouping>(Recipe.GroupedDirections);

            // Cookbook recipes are favorited in place: RecipeId is the catalog
            // row's Id, so "saved" means IsFavorite is set on that row.
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
        if (Recipe == null) return;

        try
        {
            // Flip IsFavorite on the catalog row — same mechanism the star on
            // the browse list uses, so the two stay in sync.
            await _savedRecipeService.ToggleFavoriteAsync(RecipeId);
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
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class RecipeBrowseViewModel : BaseViewModel
{
    private readonly IRecipeService _recipeService;
    private readonly ISavedRecipeService _savedRecipeService;
    private readonly IUserService _userService;

    public RecipeBrowseViewModel(
        IRecipeService recipeService,
        ISavedRecipeService savedRecipeService,
        IUserService userService)
    {
        _recipeService = recipeService;
        _savedRecipeService = savedRecipeService;
        _userService = userService;
        Title = "Cookbook";
    }

    [ObservableProperty]
    private ObservableCollection<RecipeCategory> _categories = [];

    [ObservableProperty]
    private ObservableCollection<RecipeItem> _recipes = [];

    [ObservableProperty]
    private ObservableCollection<SavedRecipe> _savedRecipes = [];

    [ObservableProperty]
    private RecipeCategory? _selectedCategory;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isShowingCategories = true;

    [ObservableProperty]
    private bool _isShowingSaved;

    /// <summary>Browse recipe list (not categories, not saved).</summary>
    [ObservableProperty]
    private bool _isShowingRecipeList;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Checking connection...";

    [ObservableProperty]
    private string _savedToggleText = "My Saved";

    [RelayCommand]
    private async Task LoadCategoriesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            IsConnected = await _recipeService.TestConnectionAsync();

            if (!IsConnected)
            {
                ConnectionStatus = "Cannot connect to recipe database. Check that SQL Server is running.";
                return;
            }

            ConnectionStatus = string.Empty;
            var cats = await _recipeService.GetCategoriesAsync();
            Categories = new ObservableCollection<RecipeCategory>(cats);
            IsShowingCategories = true;
            IsShowingRecipeList = false;
            IsShowingSaved = false;
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectCategoryAsync(RecipeCategory category)
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            SelectedCategory = category;
            var items = await _recipeService.GetRecipesByCategoryAsync(category.CategoryID);
            Recipes = new ObservableCollection<RecipeItem>(items);
            IsShowingCategories = false;
            IsShowingRecipeList = true;
            IsShowingSaved = false;
            Title = category.CategoryName;
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
    private async Task SearchAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(SearchText)) return;
        IsBusy = true;

        try
        {
            var results = await _recipeService.SearchRecipesAsync(SearchText);
            Recipes = new ObservableCollection<RecipeItem>(results);
            IsShowingCategories = false;
            IsShowingRecipeList = true;
            IsShowingSaved = false;
            SelectedCategory = null;
            Title = $"Search: {SearchText}";
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
    private async Task ViewRecipeAsync(RecipeItem recipe)
    {
        await Shell.Current.GoToAsync($"RecipeDetail?recipeId={recipe.RecipeID}");
    }

    [RelayCommand]
    private async Task ViewSavedRecipeAsync(SavedRecipe saved)
    {
        await Shell.Current.GoToAsync($"RecipeDetail?recipeId={saved.SourceRecipeId}");
    }

    [RelayCommand]
    private async Task ToggleSavedViewAsync()
    {
        if (IsShowingSaved)
        {
            // Switch back to browse
            IsShowingSaved = false;
            IsShowingRecipeList = false;
            IsShowingCategories = true;
            SavedToggleText = "My Saved";
            Title = "Cookbook";
        }
        else
        {
            // Switch to saved
            await LoadSavedRecipesAsync();
            IsShowingSaved = true;
            IsShowingRecipeList = false;
            IsShowingCategories = false;
            SavedToggleText = "Browse All";
            Title = "My Saved Recipes";
        }
    }

    private async Task LoadSavedRecipesAsync()
    {
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var saved = await _savedRecipeService.GetSavedRecipesAsync(user.Id);
            SavedRecipes = new ObservableCollection<SavedRecipe>(saved);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not load saved recipes: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private void BackToCategories()
    {
        if (IsShowingSaved)
        {
            IsShowingSaved = false;
            SavedToggleText = "My Saved";
        }

        IsShowingCategories = true;
        IsShowingRecipeList = false;
        IsShowingSaved = false;
        Title = "Cookbook";
        SearchText = string.Empty;
    }
}

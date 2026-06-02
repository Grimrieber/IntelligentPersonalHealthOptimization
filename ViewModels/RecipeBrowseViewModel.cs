using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Data;
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

    /// <summary>Top-level groups (e.g. "Main Dishes") shown first.</summary>
    [ObservableProperty]
    private ObservableCollection<RecipeCategory> _groups = [];

    /// <summary>Real categories within the selected group.</summary>
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

    /// <summary>Top level of the cookbook: the group grid.</summary>
    [ObservableProperty]
    private bool _isShowingGroups = true;

    /// <summary>Second level: the categories inside the selected group.</summary>
    [ObservableProperty]
    private bool _isShowingCategories;

    [ObservableProperty]
    private bool _isShowingSaved;

    /// <summary>Browse recipe list (not groups, not categories, not saved).</summary>
    [ObservableProperty]
    private bool _isShowingRecipeList;

    /// <summary>Whether the up-one-level back button is shown, and its label.</summary>
    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private string _backLabel = "‹ All Categories";

    // Full category list cached from the service so group drill-down filters
    // locally without re-querying. _selectedGroupName tracks the current group
    // for the recipe-list → categories back step.
    private List<RecipeCategory> _allCategories = new();
    private string _selectedGroupName = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Checking connection...";

    [ObservableProperty]
    private string _savedToggleText = "Favorites";

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
            _allCategories = (await _recipeService.GetCategoriesAsync()).ToList();

            var grouped = _allCategories
                .GroupBy(c => RecipeCategoryGroups.GroupFor(c.CategoryName))
                .Select(g => new RecipeCategory
                {
                    CategoryName = g.Key,
                    RecipeCount = g.Sum(c => c.RecipeCount),
                })
                .OrderBy(g => RecipeCategoryGroups.DisplayOrder(g.CategoryName))
                .ToList();
            Groups = new ObservableCollection<RecipeCategory>(grouped);
            ShowGroups();
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
    private void SelectGroup(RecipeCategory group)
    {
        if (group == null) return;
        ShowCategoriesForGroup(group.CategoryName);
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
            MarkSavedState(items);
            Recipes = new ObservableCollection<RecipeItem>(items);
            IsShowingGroups = false;
            IsShowingCategories = false;
            IsShowingRecipeList = true;
            IsShowingSaved = false;
            BackLabel = string.IsNullOrEmpty(_selectedGroupName) ? "‹ All Categories" : $"‹ {_selectedGroupName}";
            CanGoBack = true;
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
            MarkSavedState(results);
            Recipes = new ObservableCollection<RecipeItem>(results);
            IsShowingGroups = false;
            IsShowingCategories = false;
            IsShowingRecipeList = true;
            IsShowingSaved = false;
            SelectedCategory = null;
            _selectedGroupName = string.Empty;
            BackLabel = "‹ All Categories";
            CanGoBack = true;
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
        // saved.Id is the catalog row's primary key, which the detail page loads
        // via GetRecipeDetailAsync (NOT SourceRecipeId — that's the original
        // upstream id and won't resolve against the local catalog).
        await Shell.Current.GoToAsync($"RecipeDetail?recipeId={saved.Id}");
    }

    /// <summary>Star tap on a browse card: toggle whether this recipe is in
    /// "Favorites" by flipping IsFavorite on its catalog row. Flips IsSaved so
    /// the star glyph updates live.</summary>
    [RelayCommand]
    private async Task ToggleSaveRecipeAsync(RecipeItem recipe)
    {
        if (recipe == null) return;

        try
        {
            await _savedRecipeService.ToggleFavoriteAsync(recipe.RecipeID);
            recipe.IsSaved = !recipe.IsSaved;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not update saved recipes: {ex.Message}", "OK");
        }
    }

    /// <summary>Star tap in the "Favorites" list: un-favorite the recipe and drop
    /// it from the list (everything shown here is a favorite by definition).</summary>
    [RelayCommand]
    private async Task ToggleSavedRecipeStarAsync(SavedRecipe saved)
    {
        if (saved == null) return;

        try
        {
            await _savedRecipeService.ToggleFavoriteAsync(saved.Id);
            SavedRecipes.Remove(saved);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not update saved recipes: {ex.Message}", "OK");
        }
    }

    /// <summary>Stamp IsSaved onto a freshly-loaded recipe list so favorited
    /// recipes show a filled star. IsFavorite is already populated on each row
    /// by the recipe service, so no extra round trip is needed.</summary>
    private static void MarkSavedState(IEnumerable<RecipeItem> items)
    {
        foreach (var item in items)
            item.IsSaved = item.IsFavorite;
    }

    [RelayCommand]
    private async Task ToggleSavedViewAsync()
    {
        if (IsShowingSaved)
        {
            // Switch back to browse (top-level groups)
            ShowGroups();
        }
        else
        {
            // Switch to favorites
            await LoadSavedRecipesAsync();
            IsShowingGroups = false;
            IsShowingSaved = true;
            IsShowingRecipeList = false;
            IsShowingCategories = false;
            CanGoBack = false;
            SavedToggleText = "Browse All";
            Title = "Favorites";
        }
    }

    /// <summary>Show the top-level group grid (the cookbook's home view).</summary>
    private void ShowGroups()
    {
        IsShowingGroups = true;
        IsShowingCategories = false;
        IsShowingRecipeList = false;
        IsShowingSaved = false;
        CanGoBack = false;
        SavedToggleText = "Favorites";
        SearchText = string.Empty;
        _selectedGroupName = string.Empty;
        Title = "Cookbook";
    }

    /// <summary>Show the real categories that fall under <paramref name="groupName"/>.</summary>
    private void ShowCategoriesForGroup(string groupName)
    {
        _selectedGroupName = groupName;
        var subs = _allCategories
            .Where(c => RecipeCategoryGroups.GroupFor(c.CategoryName) == groupName)
            .OrderBy(c => c.CategoryName)
            .ToList();
        Categories = new ObservableCollection<RecipeCategory>(subs);

        IsShowingGroups = false;
        IsShowingCategories = true;
        IsShowingRecipeList = false;
        IsShowingSaved = false;
        BackLabel = "‹ All Categories";
        CanGoBack = true;
        Title = groupName;
    }

    private async Task LoadSavedRecipesAsync()
    {
        try
        {
            // "Favorites" = recipes the user starred (IsFavorite), not the whole
            // catalog. GetSavedRecipesAsync returns every Wikibooks row, so it's
            // wrong here — it's reserved for meal planning.
            var saved = await _savedRecipeService.GetFavoriteRecipesAsync();
            SavedRecipes = new ObservableCollection<SavedRecipe>(saved);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not load saved recipes: {ex.Message}", "OK");
        }
    }

    /// <summary>Up one level: recipe list → its group's categories →
    /// the top-level groups.</summary>
    [RelayCommand]
    private void BackToCategories()
    {
        if (IsShowingRecipeList && !string.IsNullOrEmpty(_selectedGroupName))
            ShowCategoriesForGroup(_selectedGroupName);   // back to the group's categories
        else
            ShowGroups();                                  // back to the top-level groups
    }
}

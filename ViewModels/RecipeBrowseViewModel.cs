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

    /// <summary>"Jump back in" strip on the Cookbook landing — the recipes the
    /// user opened most recently (see <see cref="Data.RecentRecipes"/>).</summary>
    [ObservableProperty]
    private ObservableCollection<Data.RecentRecipe> _recentlyViewed = [];

    [ObservableProperty]
    private bool _hasRecentlyViewed;

    /// <summary>Re-read the recently-viewed list from storage. Called on every
    /// page appearance so returning from a recipe refreshes the strip.</summary>
    public void RefreshRecentlyViewed()
    {
        var recent = Data.RecentRecipes.Get();
        RecentlyViewed = new ObservableCollection<Data.RecentRecipe>(recent);
        HasRecentlyViewed = recent.Count > 0;
    }

    [RelayCommand]
    private async Task OpenRecentAsync(Data.RecentRecipe recent)
    {
        if (recent == null || recent.Id <= 0) return;
        await Shell.Current.GoToAsync($"RecipeDetail?recipeId={recent.Id}");
    }

    [ObservableProperty]
    private RecipeCategory? _selectedCategory;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>"Healthy only" filter chip on the recipe list. When on, the list
    /// shows only Healthy-tier recipes (and lean treats). Global — persists across
    /// category/search navigation. See <see cref="Data.RecipeHealth"/>.</summary>
    [ObservableProperty]
    private bool _showHealthyOnly;

    // Unfiltered recipe list for the current category/search; the filters below
    // project this into the visible Recipes collection.
    private List<RecipeItem> _currentRecipes = new();

    /// <summary>Whether the macro/diet filter panel is expanded on the recipe list.</summary>
    [ObservableProperty]
    private bool _showFilters;

    /// <summary>Sort options for the recipe list.</summary>
    public List<string> SortOptions { get; } = new()
    {
        "Default", "Highest protein", "Lowest calories",
        "Highest fiber", "Lowest sugar", "Best health score", "My rating",
    };

    [ObservableProperty]
    private string _selectedSort = "Default";

    partial void OnSelectedSortChanged(string value) => ApplyRecipeFilter();

    /// <summary>Macro quick-filter chips (per serving). AND-combined with everything else.</summary>
    public ObservableCollection<FilterChip> MacroChips { get; } = new()
    {
        new("High protein", r => r.ProteinGrams >= 20),
        new("Low sugar", r => r.SugarGrams.HasValue && r.SugarGrams < 10),
        new("Under 500 cal", r => r.CaloriesPerServing.HasValue && r.CaloriesPerServing < 500),
        new("High fiber", r => r.FiberGrams >= 5),
    };

    /// <summary>Diet filter chips (multi-select, AND-combined). Backed by the
    /// precomputed per-recipe diet flags.</summary>
    public ObservableCollection<FilterChip> DietChips { get; } = new()
    {
        new("Vegetarian", r => r.IsVegetarian),
        new("Vegan", r => r.IsVegan),
        new("Pescatarian", r => r.IsPescatarian),
        new("Gluten-Free", r => r.IsGlutenFree),
        new("Dairy-Free", r => r.IsDairyFree),
        new("Keto", r => r.IsKeto),
        new("Paleo", r => r.IsPaleo),
        new("Halal", r => r.IsHalal),
        new("Kosher", r => r.IsKosher),
        new("Mediterranean", r => r.IsMediterranean),
    };

    /// <summary>Number of active filters — drives the "Filters (N)" button label.</summary>
    public int ActiveFilterCount =>
        (ShowHealthyOnly ? 1 : 0)
        + (SelectedSort != "Default" ? 1 : 0)
        + MacroChips.Count(c => c.IsSelected)
        + DietChips.Count(c => c.IsSelected);

    public string FilterButtonText => ActiveFilterCount > 0 ? $"Filters ({ActiveFilterCount})" : "Filters";
    public bool HasActiveFilters => ActiveFilterCount > 0;

    [RelayCommand]
    private void ToggleFilters() => ShowFilters = !ShowFilters;

    [RelayCommand]
    private void ToggleChip(FilterChip chip)
    {
        if (chip == null) return;
        chip.IsSelected = !chip.IsSelected;
        ApplyRecipeFilter();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        ShowHealthyOnly = false;
        SelectedSort = "Default";
        foreach (var c in MacroChips) c.IsSelected = false;
        foreach (var c in DietChips) c.IsSelected = false;
        ApplyRecipeFilter();
    }

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
            _currentRecipes = items;
            ApplyRecipeFilter();
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

    // Re-apply the health filter to the visible list when the toggle flips.
    partial void OnShowHealthyOnlyChanged(bool value) => ApplyRecipeFilter();

    /// <summary>Project the current unfiltered list into the visible Recipes
    /// collection, applying the Healthy toggle, macro/diet chips, and sort. All
    /// filters AND-combine; they persist across category/search navigation.</summary>
    private void ApplyRecipeFilter()
    {
        IEnumerable<RecipeItem> src = _currentRecipes;

        if (ShowHealthyOnly)
            src = src.Where(r => r.IsHealthy || r.IsHealthyTreat);

        // Macro + diet chips: every selected chip must match (AND).
        foreach (var chip in MacroChips.Where(c => c.IsSelected))
            src = src.Where(chip.Match);
        foreach (var chip in DietChips.Where(c => c.IsSelected))
            src = src.Where(chip.Match);

        src = SelectedSort switch
        {
            "Highest protein" => src.OrderByDescending(r => r.ProteinGrams ?? -1),
            "Lowest calories" => src.OrderBy(r => r.CaloriesPerServing ?? int.MaxValue),
            "Highest fiber" => src.OrderByDescending(r => r.FiberGrams ?? -1),
            "Lowest sugar" => src.OrderBy(r => r.SugarGrams ?? double.MaxValue),
            "Best health score" => src.OrderByDescending(r => r.HealthScore ?? -1),
            "My rating" => src.OrderByDescending(r => r.MyRating).ThenBy(r => r.RecipeName),
            _ => src, // Default: keep the incoming order (alphabetical from the query).
        };

        Recipes = new ObservableCollection<RecipeItem>(src);
        OnPropertyChanged(nameof(ActiveFilterCount));
        OnPropertyChanged(nameof(FilterButtonText));
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    private CancellationTokenSource? _searchDebounce;

    // Search as you type (debounced) — the redesign dropped the explicit Search
    // button, so typing must surface results on its own. Clearing the box (or
    // tapping Browse, which empties it) just cancels; the view is handled elsewhere.
    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce?.Cancel();
        if (string.IsNullOrWhiteSpace(value)) return;

        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token);
                if (!token.IsCancellationRequested)
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!token.IsCancellationRequested && !string.IsNullOrWhiteSpace(SearchText))
                            SearchCommand.Execute(null);
                    });
            }
            catch (TaskCanceledException) { }
        });
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
            _currentRecipes = results;
            ApplyRecipeFilter();
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

    /// <summary>Load the ENTIRE catalog into the recipe list so the macro/diet
    /// filters and sort apply across every category at once.</summary>
    [RelayCommand]
    private async Task ShowAllRecipesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var items = await _recipeService.GetAllRecipesAsync();
            MarkSavedState(items);
            _currentRecipes = items;
            ApplyRecipeFilter();
            IsShowingGroups = false;
            IsShowingCategories = false;
            IsShowingRecipeList = true;
            IsShowingSaved = false;
            SelectedCategory = null;
            _selectedGroupName = string.Empty;
            BackLabel = "‹ All Categories";
            CanGoBack = true;
            ShowFilters = true; // reveal the filter panel — that's the point of this view
            Title = "All Recipes";
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

    /// <summary>Segmented-control "Browse" tab: always return to the cookbook home
    /// (top-level groups), whether we're in Favorites or drilled into a category/search.</summary>
    [RelayCommand]
    private void ShowBrowse()
    {
        ShowGroups();
    }

    /// <summary>Segmented-control "Favorites" tab: show saved recipes. No-op if already there.</summary>
    [RelayCommand]
    private async Task ShowFavoritesAsync()
    {
        if (IsShowingSaved) return;
        await LoadSavedRecipesAsync();
        IsShowingGroups = false;
        IsShowingSaved = true;
        IsShowingRecipeList = false;
        IsShowingCategories = false;
        CanGoBack = false;
        SavedToggleText = "Browse All";
        Title = "Favorites";
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

/// <summary>A toggleable filter chip (macro quick-filter or diet) in the cookbook.
/// <see cref="Match"/> is the predicate a recipe must satisfy when the chip is on.</summary>
public partial class FilterChip : ObservableObject
{
    public string Label { get; }
    public Func<RecipeItem, bool> Match { get; }

    [ObservableProperty]
    private bool _isSelected;

    public FilterChip(string label, Func<RecipeItem, bool> match)
    {
        Label = label;
        Match = match;
    }
}

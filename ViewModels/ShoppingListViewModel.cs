using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Data;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

/// <summary>
/// Builds a consolidated grocery list from the user's active meal plan: every
/// recipe's ingredients, de-duplicated by ingredient and grouped into aisles
/// (see <see cref="GroceryList"/>). Items are checkable so it works as a live
/// shopping list. Recipes repeat across the plan (meal-prep), so recipes are
/// de-duplicated first — the list is "everything you need to cook this plan."
/// </summary>
public partial class ShoppingListViewModel : BaseViewModel
{
    private readonly INutritionService _nutritionService;
    private readonly ISavedRecipeService _savedRecipeService;
    private readonly IUserService _userService;

    public ShoppingListViewModel(
        INutritionService nutritionService,
        ISavedRecipeService savedRecipeService,
        IUserService userService)
    {
        _nutritionService = nutritionService;
        _savedRecipeService = savedRecipeService;
        _userService = userService;
        Title = "Shopping List";
    }

    [ObservableProperty]
    private ObservableCollection<GroceryAisle> _aisles = new();

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _hasExtraItems;

    private readonly List<GroceryItem> _allItems = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            _allItems.Clear();
            Aisles = new ObservableCollection<GroceryAisle>();

            var user = await _userService.GetCurrentUserAsync();
            if (user == null) { Finish(); return; }

            // Gather every ingredient line, tagged with its recipe.
            var lines = new List<(string recipe, string description)>();

            var plan = await _nutritionService.GetActiveMealPlanAsync(user.Id);
            if (plan != null)
            {
                // Collect the unique catalog recipes used across the plan, keeping a
                // display name for each (from the meal item).
                var recipeNames = new Dictionary<int, string>();
                var days = await _nutritionService.GetMealPlanDaysAsync(plan.Id);
                foreach (var day in days)
                {
                    var items = await _nutritionService.GetMealItemsAsync(day.Id);
                    foreach (var it in items)
                    {
                        if (it.SavedRecipeId > 0 && !recipeNames.ContainsKey(it.SavedRecipeId))
                            recipeNames[it.SavedRecipeId] = string.IsNullOrWhiteSpace(it.MealName)
                                ? "Recipe" : it.MealName;
                    }
                }

                foreach (var (recipeId, name) in recipeNames)
                {
                    var ingredients = await _savedRecipeService.GetSavedIngredientsAsync(recipeId);
                    foreach (var ing in ingredients)
                        if (!string.IsNullOrWhiteSpace(ing.Description))
                            lines.Add((name, ing.Description));
                }
            }

            // Merge in any ad-hoc items added from recipes' "Add to Shopping List".
            var extra = Data.ExtraShoppingItems.Get();
            HasExtraItems = extra.Count > 0;
            foreach (var x in extra)
                lines.Add((x.Recipe, x.Description));

            var entries = GroceryList.Build(lines);

            var grouped = new ObservableCollection<GroceryAisle>();
            foreach (var aisleName in GroceryList.AisleOrder)
            {
                var aisleEntries = entries.Where(e => e.Aisle == aisleName).ToList();
                if (aisleEntries.Count == 0) continue;

                var aisle = new GroceryAisle
                {
                    Name = aisleName,
                    Emoji = GroceryList.AisleEmoji(aisleName),
                };
                foreach (var e in aisleEntries)
                {
                    var item = new GroceryItem
                    {
                        Name = e.Name,
                        Detail = BuildDetail(e),
                    };
                    item.CheckedChanged = UpdateSummary;
                    aisle.Items.Add(item);
                    _allItems.Add(item);
                }
                grouped.Add(aisle);
            }

            Aisles = grouped;
            Finish();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("ShoppingList load", ex);
            Finish();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildDetail(GroceryList.Entry e)
    {
        // Show the raw amounts; attribute to the recipe only when a single recipe
        // needs it (otherwise list the amounts, which already read clearly).
        var amounts = e.Amounts.Distinct().ToList();
        var text = string.Join("  •  ", amounts.Take(4));
        if (amounts.Count > 4) text += $"  •  +{amounts.Count - 4} more";
        return text;
    }

    private void Finish()
    {
        IsEmpty = _allItems.Count == 0;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var total = _allItems.Count;
        var got = _allItems.Count(i => i.IsChecked);
        Summary = total == 0
            ? "Nothing to shop for yet — add a recipe's ingredients from its page, or generate a meal plan."
            : $"{got}/{total} items checked";
    }

    /// <summary>Remove the ad-hoc items added from recipe pages (keeps meal-plan items).</summary>
    [RelayCommand]
    private async Task ClearAddedItemsAsync()
    {
        Data.ExtraShoppingItems.Clear();
        HasExtraItems = false;
        await LoadAsync();
    }

    [RelayCommand]
    private void ToggleItem(GroceryItem item)
    {
        if (item == null) return;
        item.IsChecked = !item.IsChecked;
    }

    [RelayCommand]
    private void ClearChecks()
    {
        foreach (var i in _allItems) i.IsChecked = false;
        UpdateSummary();
    }
}

/// <summary>A grocery aisle section with its items.</summary>
public class GroceryAisle
{
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public ObservableCollection<GroceryItem> Items { get; } = new();
}

/// <summary>A single checkable line on the shopping list.</summary>
public partial class GroceryItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;

    /// <summary>Invoked by the VM when IsChecked flips, to refresh the summary.</summary>
    public Action? CheckedChanged { get; set; }

    [ObservableProperty]
    private bool _isChecked;

    partial void OnIsCheckedChanged(bool value) => CheckedChanged?.Invoke();
}

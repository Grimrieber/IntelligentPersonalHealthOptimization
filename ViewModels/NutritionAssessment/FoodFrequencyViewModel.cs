using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class FoodFrequencyViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;
    private bool _isInitialized;

    // Shared across all items to avoid 33 separate allocations
    private static readonly List<FoodFrequency> SharedFrequencyOptions =
        Enum.GetValues<FoodFrequency>().ToList();

    private static readonly List<(string Name, FFQCategory Category)> FoodItems =
    [
        ("Water", FFQCategory.Beverages),
        ("Soft drinks / Soda", FFQCategory.Beverages),
        ("Fruit juice", FFQCategory.Beverages),
        ("Coffee / Tea", FFQCategory.Beverages),
        ("Alcohol", FFQCategory.Beverages),
        ("Fresh fruit", FFQCategory.Fruits),
        ("Dried fruit", FFQCategory.Fruits),
        ("Canned / Frozen fruit", FFQCategory.Fruits),
        ("Leafy greens (spinach, lettuce)", FFQCategory.Vegetables),
        ("Starchy vegetables (potatoes, corn)", FFQCategory.Vegetables),
        ("Other vegetables (broccoli, peppers)", FFQCategory.Vegetables),
        ("Whole grain bread", FFQCategory.Grains),
        ("White bread / Pasta", FFQCategory.Grains),
        ("Rice", FFQCategory.Grains),
        ("Cereal / Oatmeal", FFQCategory.Grains),
        ("Chicken / Turkey", FFQCategory.Protein),
        ("Red meat (beef, pork, lamb)", FFQCategory.Protein),
        ("Fish / Seafood", FFQCategory.Protein),
        ("Eggs", FFQCategory.Protein),
        ("Beans / Legumes", FFQCategory.Protein),
        ("Milk", FFQCategory.Dairy),
        ("Yogurt", FFQCategory.Dairy),
        ("Cheese", FFQCategory.Dairy),
        ("Cooking oils (olive, coconut)", FFQCategory.FatsAndOils),
        ("Nuts / Seeds", FFQCategory.FatsAndOils),
        ("Butter / Margarine", FFQCategory.FatsAndOils),
        ("Fast food", FFQCategory.Processed),
        ("Processed meats (hot dogs, bacon)", FFQCategory.Processed),
        ("Chips / Crackers", FFQCategory.Processed),
        ("Frozen meals", FFQCategory.Processed),
        ("Candy / Chocolate", FFQCategory.Sweets),
        ("Baked goods (cookies, cakes)", FFQCategory.Sweets),
        ("Ice cream / Frozen desserts", FFQCategory.Sweets)
    ];

    public FoodFrequencyViewModel(INutritionAssessmentCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public ObservableCollection<FFQItemViewModel> FoodResponses { get; } = [];
    public ObservableCollection<FFQCategoryGroup> CategoryGroups { get; } = [];

    public List<FoodFrequency> FrequencyOptions => SharedFrequencyOptions;

    /// <summary>
    /// Called from OnAppearing to defer heavy initialization off the constructor.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        // Build items on background thread
        var items = await Task.Run(() =>
        {
            var list = new List<FFQItemViewModel>();
            var existing = _coordinator.Data.FoodFrequencyResponses;

            if (existing.Count > 0)
            {
                foreach (var response in existing)
                {
                    list.Add(new FFQItemViewModel
                    {
                        FoodName = response.FoodName,
                        Category = response.Category,
                        SelectedFrequency = response.Frequency,
                        FrequencyOptions = SharedFrequencyOptions
                    });
                }
            }
            else
            {
                foreach (var (name, category) in FoodItems)
                {
                    list.Add(new FFQItemViewModel
                    {
                        FoodName = name,
                        Category = category,
                        SelectedFrequency = FoodFrequency.Never,
                        FrequencyOptions = SharedFrequencyOptions
                    });
                }
            }

            return list;
        });

        // Add to observable collections on UI thread
        foreach (var item in items)
            FoodResponses.Add(item);

        foreach (var category in Enum.GetValues<FFQCategory>())
        {
            var catItems = FoodResponses.Where(r => r.Category == category).ToList();
            if (catItems.Count > 0)
            {
                CategoryGroups.Add(new FFQCategoryGroup
                {
                    CategoryName = FormatCategory(category),
                    Items = new ObservableCollection<FFQItemViewModel>(catItems)
                });
            }
        }
    }

    public void SyncToCoordinator()
    {
        _coordinator.Data.FoodFrequencyResponses = FoodResponses.Select(r => new FoodFrequencyResponse
        {
            FoodName = r.FoodName,
            Category = r.Category,
            Frequency = r.SelectedFrequency
        }).ToList();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        SyncToCoordinator();
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        SyncToCoordinator();
        await _coordinator.GoPreviousAsync();
    }

    private static string FormatCategory(FFQCategory category) => category switch
    {
        FFQCategory.FatsAndOils => "Fats & Oils",
        _ => category.ToString()
    };
}

public partial class FFQItemViewModel : ObservableObject
{
    public string FoodName { get; init; } = string.Empty;
    public FFQCategory Category { get; init; }

    // Shared list reference — set by the parent ViewModel to avoid per-item allocations
    public List<FoodFrequency> FrequencyOptions { get; init; } = [];

    [ObservableProperty] private FoodFrequency _selectedFrequency;
}

public class FFQCategoryGroup
{
    public string CategoryName { get; init; } = string.Empty;
    public ObservableCollection<FFQItemViewModel> Items { get; init; } = [];
}

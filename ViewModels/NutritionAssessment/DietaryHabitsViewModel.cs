using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class DietaryHabitsViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;

    private bool _isInitialized;

    public DietaryHabitsViewModel(INutritionAssessmentCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        EatingPatternItems = CreateEatingPatterns();
        EatingBehaviorItems = CreateEatingBehaviors();
    }

    public void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        foreach (var item in EatingPatternItems)
            item.IsChecked = _coordinator.Data.SelectedEatingPatterns.Contains(item.Value);
        foreach (var item in EatingBehaviorItems)
            item.IsChecked = _coordinator.Data.SelectedEatingBehaviors.Contains(item.Value);
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<CheckableItem<EatingPattern>> EatingPatternItems { get; }
    public List<CheckableItem<EatingBehavior>> EatingBehaviorItems { get; }

    public void SyncToCoordinator()
    {
        _coordinator.Data.SelectedEatingPatterns = EatingPatternItems
            .Where(i => i.IsChecked).Select(i => i.Value).ToList();
        _coordinator.Data.SelectedEatingBehaviors = EatingBehaviorItems
            .Where(i => i.IsChecked).Select(i => i.Value).ToList();
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

    private static List<CheckableItem<EatingPattern>> CreateEatingPatterns() =>
    [
        new() { Value = EatingPattern.EatBreakfastRegularly, DisplayName = "Eat breakfast regularly", Description = "You eat breakfast most days of the week" },
        new() { Value = EatingPattern.SkipBreakfast, DisplayName = "Skip breakfast", Description = "You often skip breakfast or just have coffee" },
        new() { Value = EatingPattern.EatLunchRegularly, DisplayName = "Eat lunch regularly", Description = "You eat lunch at a consistent time" },
        new() { Value = EatingPattern.SkipLunch, DisplayName = "Skip lunch", Description = "You often miss or skip lunch" },
        new() { Value = EatingPattern.EatDinnerRegularly, DisplayName = "Eat dinner regularly", Description = "You eat dinner at a consistent time" },
        new() { Value = EatingPattern.EatLateAtNight, DisplayName = "Eat late at night", Description = "You regularly eat after 9 PM" },
        new() { Value = EatingPattern.EatAtConsistentTimes, DisplayName = "Eat at consistent times", Description = "Your meals happen at roughly the same time daily" },
        new() { Value = EatingPattern.GrazeAllDay, DisplayName = "Graze throughout the day", Description = "You eat small amounts frequently rather than set meals" }
    ];

    private static List<CheckableItem<EatingBehavior>> CreateEatingBehaviors() =>
    [
        new() { Value = EatingBehavior.StressEmotionalEating, DisplayName = "Stress / emotional eating", Description = "You eat more when stressed, anxious, or emotional" },
        new() { Value = EatingBehavior.EatOutFrequently, DisplayName = "Eat out frequently", Description = "You eat at restaurants 3+ times per week" },
        new() { Value = EatingBehavior.CookAtHome, DisplayName = "Cook at home", Description = "You prepare most of your meals at home" },
        new() { Value = EatingBehavior.EatOnTheGo, DisplayName = "Eat on the go", Description = "You often eat while commuting or multitasking" },
        new() { Value = EatingBehavior.TendToOvereat, DisplayName = "Tend to overeat", Description = "You often eat beyond feeling full" },
        new() { Value = EatingBehavior.SnackFrequently, DisplayName = "Snack frequently", Description = "You snack between meals most days" },
        new() { Value = EatingBehavior.DrinkSugaryBeverages, DisplayName = "Drink sugary beverages", Description = "You regularly drink soda, juice, or sweetened drinks" },
        new() { Value = EatingBehavior.DrinkCaffeineDaily, DisplayName = "Drink caffeine daily", Description = "You consume coffee, tea, or energy drinks daily" },
        new() { Value = EatingBehavior.DrinkAlcoholRegularly, DisplayName = "Drink alcohol regularly", Description = "You consume alcohol 3+ times per week" },
        new() { Value = EatingBehavior.MealPrep, DisplayName = "Meal prep", Description = "You prepare meals ahead of time for the week" },
        new() { Value = EatingBehavior.ReadNutritionLabels, DisplayName = "Read nutrition labels", Description = "You check labels before buying food" },
        new() { Value = EatingBehavior.RelyOnConvenienceFoods, DisplayName = "Rely on convenience foods", Description = "You often eat packaged, frozen, or takeout meals" },
        new() { Value = EatingBehavior.EatWhileDistracted, DisplayName = "Eat while distracted", Description = "You eat while watching TV, phone, or working" },
        new() { Value = EatingBehavior.FollowRecipes, DisplayName = "Follow recipes", Description = "You cook using recipes and measured ingredients" }
    ];
}

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
    }

    public void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        foreach (var item in EatingPatternItems)
            item.IsChecked = _coordinator.Data.SelectedEatingPatterns.Contains(item.Value);
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<CheckableItem<EatingPattern>> EatingPatternItems { get; }

    public void SyncToCoordinator()
    {
        _coordinator.Data.SelectedEatingPatterns = EatingPatternItems
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
}

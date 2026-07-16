using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class BehavioralReadinessViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;

    private static readonly Dictionary<NutritionMotivation, string> MotivationLabels = new()
    {
        [NutritionMotivation.MoreEnergy] = "More energy",
        [NutritionMotivation.WeightLoss] = "Weight loss",
        [NutritionMotivation.BuildMuscle] = "Build muscle",
        [NutritionMotivation.BetterHealth] = "Better overall health",
        [NutritionMotivation.AthleticPerformance] = "Athletic performance",
        [NutritionMotivation.ManageHealthCondition] = "Manage a health condition",
        [NutritionMotivation.BetterSleep] = "Better sleep",
        [NutritionMotivation.ImprovedMood] = "Improved mood",
        [NutritionMotivation.FamilyRoleModel] = "Be a role model for family",
        [NutritionMotivation.BodyConfidence] = "Body confidence"
    };

    private static readonly Dictionary<NutritionChallenge, string> ChallengeLabels = new()
    {
        [NutritionChallenge.BusySchedule] = "Busy schedule",
        [NutritionChallenge.LimitedCookingSkills] = "Limited cooking skills",
        [NutritionChallenge.BudgetConstraints] = "Budget constraints",
        [NutritionChallenge.Cravings] = "Cravings",
        [NutritionChallenge.EmotionalEating] = "Emotional eating",
        [NutritionChallenge.SocialPressure] = "Social pressure",
        [NutritionChallenge.LackOfSupport] = "Lack of support",
        [NutritionChallenge.ConfusionAboutDiet] = "Confusion about what to eat",
        [NutritionChallenge.InconsistentSchedule] = "Inconsistent schedule",
        [NutritionChallenge.HealthConditionLimits] = "Health condition limits",
        [NutritionChallenge.LimitedFoodAccess] = "Limited food access",
        [NutritionChallenge.Motivation] = "Staying motivated"
    };

    public BehavioralReadinessViewModel(INutritionAssessmentCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        _confidenceLevel = _coordinator.Data.ConfidenceLevel;

        foreach (var m in _coordinator.Data.SelectedMotivations)
            SelectedMotivations.Add(m);

        foreach (var c in _coordinator.Data.SelectedChallenges)
            SelectedChallenges.Add(c);

        // Inline toggle chips (tap to select/deselect) instead of add-picker + pills.
        foreach (var m in Enum.GetValues<NutritionMotivation>())
            MotivationChips.Add(new Models.ToggleChip
            { Value = m, Label = GetMotivationLabel(m), IsSelected = SelectedMotivations.Contains(m) });
        foreach (var c in Enum.GetValues<NutritionChallenge>())
            ChallengeChips.Add(new Models.ToggleChip
            { Value = c, Label = GetChallengeLabel(c), IsSelected = SelectedChallenges.Contains(c) });
    }

    public ObservableCollection<Models.ToggleChip> MotivationChips { get; } = [];
    public ObservableCollection<Models.ToggleChip> ChallengeChips { get; } = [];

    [RelayCommand]
    private void ToggleMotivationChip(Models.ToggleChip? chip)
    {
        if (chip?.Value is not NutritionMotivation m) return;
        if (chip.IsSelected) { SelectedMotivations.Remove(m); chip.IsSelected = false; }
        else { if (!SelectedMotivations.Contains(m)) SelectedMotivations.Add(m); chip.IsSelected = true; }
    }

    [RelayCommand]
    private void ToggleChallengeChip(Models.ToggleChip? chip)
    {
        if (chip?.Value is not NutritionChallenge c) return;
        if (chip.IsSelected) { SelectedChallenges.Remove(c); chip.IsSelected = false; }
        else { if (!SelectedChallenges.Contains(c)) SelectedChallenges.Add(c); chip.IsSelected = true; }
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfidenceDisplay))]
    private int _confidenceLevel;

    public string ConfidenceDisplay => $"{ConfidenceLevel}/10";

    public ObservableCollection<NutritionMotivation> SelectedMotivations { get; } = [];
    public ObservableCollection<NutritionChallenge> SelectedChallenges { get; } = [];

    public static string GetMotivationLabel(NutritionMotivation m) =>
        MotivationLabels.TryGetValue(m, out var label) ? label : m.ToString();

    public static string GetChallengeLabel(NutritionChallenge c) =>
        ChallengeLabels.TryGetValue(c, out var label) ? label : c.ToString();

    [RelayCommand]
    private async Task AddMotivationAsync()
    {
        var unselected = Enum.GetValues<NutritionMotivation>()
            .Where(m => !SelectedMotivations.Contains(m))
            .Select(m => GetMotivationLabel(m))
            .ToArray();
        if (unselected.Length == 0) return;

        var result = await Shell.Current.DisplayActionSheet("What motivates you?", "Cancel", null, unselected);
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        var match = MotivationLabels.FirstOrDefault(kv => kv.Value == result).Key;
        if (!SelectedMotivations.Contains(match))
            SelectedMotivations.Add(match);
    }

    [RelayCommand]
    private void RemoveMotivation(NutritionMotivation motivation) =>
        SelectedMotivations.Remove(motivation);

    [RelayCommand]
    private async Task AddChallengeAsync()
    {
        var unselected = Enum.GetValues<NutritionChallenge>()
            .Where(c => !SelectedChallenges.Contains(c))
            .Select(c => GetChallengeLabel(c))
            .ToArray();
        if (unselected.Length == 0) return;

        var result = await Shell.Current.DisplayActionSheet("Select a challenge", "Cancel", null, unselected);
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        var match = ChallengeLabels.FirstOrDefault(kv => kv.Value == result).Key;
        if (!SelectedChallenges.Contains(match))
            SelectedChallenges.Add(match);
    }

    [RelayCommand]
    private void RemoveChallenge(NutritionChallenge challenge) =>
        SelectedChallenges.Remove(challenge);

    public void SyncToCoordinator()
    {
        _coordinator.Data.ConfidenceLevel = ConfidenceLevel;
        _coordinator.Data.SelectedMotivations = [.. SelectedMotivations];
        _coordinator.Data.SelectedChallenges = [.. SelectedChallenges];
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
}

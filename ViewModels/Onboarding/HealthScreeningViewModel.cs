using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class HealthScreeningViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    public HealthScreeningViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        // Load existing selections
        foreach (var condition in _coordinator.Data.MedicalConditions)
            SelectedConditions.Add(condition);

        foreach (var injury in _coordinator.Data.InjuryAreas)
            SelectedInjuries.Add(injury);

        // Inline toggle chips (tap to select/deselect) instead of add-picker + pills.
        foreach (var c in AvailableConditions)
            ConditionChips.Add(new Models.ToggleChip { Value = c, Label = c, IsSelected = SelectedConditions.Contains(c) });
        foreach (var i in AvailableInjuries)
            InjuryChips.Add(new Models.ToggleChip { Value = i, Label = i, IsSelected = SelectedInjuries.Contains(i) });
    }

    public ObservableCollection<Models.ToggleChip> ConditionChips { get; } = [];
    public ObservableCollection<Models.ToggleChip> InjuryChips { get; } = [];

    /// <summary>Tap a condition chip. "None" is mutually exclusive with the rest.</summary>
    [RelayCommand]
    private void ToggleConditionChip(Models.ToggleChip? chip) =>
        ToggleStringChip(chip, SelectedConditions, ConditionChips, SyncConditions);

    [RelayCommand]
    private void ToggleInjuryChip(Models.ToggleChip? chip) =>
        ToggleStringChip(chip, SelectedInjuries, InjuryChips, SyncInjuries);

    // Shared toggle logic for the string-list multi-selects (conditions/injuries).
    private static void ToggleStringChip(Models.ToggleChip? chip,
        ObservableCollection<string> selected, ObservableCollection<Models.ToggleChip> chips, Action sync)
    {
        if (chip?.Value is not string value) return;
        var select = !chip.IsSelected;

        if (value == "None")
        {
            if (select)
            {
                selected.Clear();
                selected.Add("None");
                foreach (var c in chips) c.IsSelected = (string?)c.Value == "None";
            }
            else { selected.Remove("None"); chip.IsSelected = false; }
        }
        else if (select)
        {
            selected.Remove("None");
            var none = chips.FirstOrDefault(c => (string?)c.Value == "None");
            if (none != null) none.IsSelected = false;
            if (!selected.Contains(value)) selected.Add(value);
            chip.IsSelected = true;
        }
        else { selected.Remove(value); chip.IsSelected = false; }

        sync();
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<string> AvailableConditions { get; } =
    [
        "High Blood Pressure",
        "Heart Disease",
        "Type 1 Diabetes",
        "Type 2 Diabetes",
        "Asthma",
        "Arthritis",
        "Chronic Back Pain",
        "Depression",
        "Anxiety",
        "Pregnancy",
        "None"
    ];

    public List<string> AvailableInjuries { get; } =
    [
        "Shoulder",
        "Lower Back",
        "Knee",
        "Ankle/Foot",
        "Hip",
        "Neck",
        "Wrist/Hand",
        "Upper Back",
        "None"
    ];

    public ObservableCollection<string> SelectedConditions { get; } = [];
    public ObservableCollection<string> SelectedInjuries { get; } = [];

    [ObservableProperty]
    private string _conditionsSummary = "No conditions selected";

    [ObservableProperty]
    private string _injuriesSummary = "No injuries selected";

    [RelayCommand]
    private async Task AddConditionAsync()
    {
        var unselected = AvailableConditions.Where(c => !SelectedConditions.Contains(c)).ToArray();
        if (unselected.Length == 0) return;

        var result = await Shell.Current.DisplayActionSheet("Select Medical Condition", "Cancel", null, unselected);
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        if (result == "None")
        {
            SelectedConditions.Clear();
            SelectedConditions.Add("None");
        }
        else
        {
            SelectedConditions.Remove("None");
            if (!SelectedConditions.Contains(result))
                SelectedConditions.Add(result);
        }

        SyncConditions();
    }

    [RelayCommand]
    private void RemoveCondition(string condition)
    {
        SelectedConditions.Remove(condition);
        SyncConditions();
    }

    [RelayCommand]
    private async Task AddInjuryAsync()
    {
        var unselected = AvailableInjuries.Where(i => !SelectedInjuries.Contains(i)).ToArray();
        if (unselected.Length == 0) return;

        var result = await Shell.Current.DisplayActionSheet("Select Injury Area", "Cancel", null, unselected);
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        if (result == "None")
        {
            SelectedInjuries.Clear();
            SelectedInjuries.Add("None");
        }
        else
        {
            SelectedInjuries.Remove("None");
            if (!SelectedInjuries.Contains(result))
                SelectedInjuries.Add(result);
        }

        SyncInjuries();
    }

    [RelayCommand]
    private void RemoveInjury(string injury)
    {
        SelectedInjuries.Remove(injury);
        SyncInjuries();
    }

    private void SyncConditions()
    {
        _coordinator.Data.MedicalConditions = [.. SelectedConditions];
        ConditionsSummary = SelectedConditions.Count > 0
            ? string.Join(", ", SelectedConditions)
            : "No conditions selected";
    }

    private void SyncInjuries()
    {
        _coordinator.Data.InjuryAreas = [.. SelectedInjuries];
        InjuriesSummary = SelectedInjuries.Count > 0
            ? string.Join(", ", SelectedInjuries)
            : "No injuries selected";
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }
}

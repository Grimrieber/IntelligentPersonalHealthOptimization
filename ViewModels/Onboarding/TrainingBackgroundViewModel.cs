using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Helpers;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class TrainingBackgroundViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    public TrainingBackgroundViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        _selectedTrainingLocation = TrainingLocationOptions.FirstOrDefault(o => o.Value == _coordinator.Data.TrainingLocation) ?? TrainingLocationOptions[0];
        _selectedExperienceLevel = ExperienceLevels.FirstOrDefault(o => o.Value == _coordinator.Data.ExperienceLevel) ?? ExperienceLevels[0];
        _currentFrequency = _coordinator.Data.CurrentFrequency;
        _sessionDurationMinutes = _coordinator.Data.SessionDurationMinutes;
        _selectedTimeOfDay = _coordinator.Data.PreferredTimeOfDay;

        // Load equipment selections
        foreach (var eq in _coordinator.Data.AvailableEquipment)
            SelectedEquipment.Add(eq);

        // Inline toggle chips (tap to select/deselect) instead of add-picker + pills.
        foreach (var e in AllEquipment)
            EquipmentChips.Add(new Models.ToggleChip
            { Value = e, Label = Helpers.EnumDisplay.Humanize(e.ToString()), IsSelected = SelectedEquipment.Contains(e) });

        // Seed the day selector CSV (assign the backing field so we don't trigger a save here).
        _availableDaysCsv = string.Join(",", _coordinator.Data.AvailableDays);
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<PickerItem<ExperienceLevel>> ExperienceLevels { get; } =
        PickerItem<ExperienceLevel>.From(
            [ExperienceLevel.Beginner, ExperienceLevel.Novice, ExperienceLevel.Intermediate, ExperienceLevel.Advanced, ExperienceLevel.Elite]);

    public List<PickerItem<TrainingLocation>> TrainingLocationOptions { get; } =
        PickerItem<TrainingLocation>.From([TrainingLocation.Gym, TrainingLocation.Home, TrainingLocation.Both]);

    public List<string> TimeOfDayOptions { get; } = ["Morning", "Afternoon", "Evening"];

    public List<EquipmentType> AllEquipment { get; } =
        Enum.GetValues<EquipmentType>().ToList();

    [ObservableProperty]
    private PickerItem<TrainingLocation> _selectedTrainingLocation;

    [ObservableProperty]
    private PickerItem<ExperienceLevel> _selectedExperienceLevel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrequencyDisplay))]
    private int _currentFrequency;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationDisplay))]
    private int _sessionDurationMinutes;

    [ObservableProperty]
    private string _selectedTimeOfDay;

    // CSV of selected weekday names, bound to the shared TrainingDaySelector control.
    [ObservableProperty]
    private string _availableDaysCsv = string.Empty;

    public string FrequencyDisplay => $"{CurrentFrequency} days/week";
    public string DurationDisplay => $"{SessionDurationMinutes} minutes";

    public ObservableCollection<EquipmentType> SelectedEquipment { get; } = [];
    public ObservableCollection<Models.ToggleChip> EquipmentChips { get; } = [];

    [RelayCommand]
    private void ToggleEquipmentChip(Models.ToggleChip? chip)
    {
        if (chip?.Value is not EquipmentType e) return;
        if (chip.IsSelected) { SelectedEquipment.Remove(e); chip.IsSelected = false; }
        else { if (!SelectedEquipment.Contains(e)) SelectedEquipment.Add(e); chip.IsSelected = true; }
        SyncEquipment();
    }

    // Re-point each chip's selected state at SelectedEquipment (after location auto-fill).
    private void SyncEquipmentChips()
    {
        foreach (var c in EquipmentChips)
            c.IsSelected = c.Value is EquipmentType e && SelectedEquipment.Contains(e);
    }

    partial void OnSelectedTrainingLocationChanged(PickerItem<TrainingLocation> value)
    {
        _coordinator.Data.TrainingLocation = value.Value;
        ApplyEquipmentSuggestions(value.Value);
    }

    partial void OnSelectedExperienceLevelChanged(PickerItem<ExperienceLevel> value) =>
        _coordinator.Data.ExperienceLevel = value.Value;
    partial void OnCurrentFrequencyChanged(int value) => _coordinator.Data.CurrentFrequency = value;
    partial void OnSessionDurationMinutesChanged(int value) => _coordinator.Data.SessionDurationMinutes = value;
    partial void OnSelectedTimeOfDayChanged(string value) => _coordinator.Data.PreferredTimeOfDay = value;

    partial void OnAvailableDaysCsvChanged(string value) =>
        _coordinator.Data.AvailableDays = [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    [RelayCommand]
    private async Task ToggleEquipmentAsync()
    {
        var unselected = AllEquipment.Where(e => !SelectedEquipment.Contains(e))
            .Select(e => e.ToString()).ToArray();

        if (unselected.Length == 0) return;

        var result = await Shell.Current.DisplayActionSheet("Add Equipment", "Cancel", null, unselected);
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        if (Enum.TryParse<EquipmentType>(result, out var equipment))
        {
            if (!SelectedEquipment.Contains(equipment))
                SelectedEquipment.Add(equipment);
        }

        SyncEquipment();
    }

    [RelayCommand]
    private void RemoveEquipment(EquipmentType equipment)
    {
        SelectedEquipment.Remove(equipment);
        SyncEquipment();
    }

    private void ApplyEquipmentSuggestions(TrainingLocation location)
    {
        var suggested = location switch
        {
            TrainingLocation.Gym =>
            [
                EquipmentType.Barbell, EquipmentType.Cable, EquipmentType.Machine,
                EquipmentType.Bench, EquipmentType.PullUpBar, EquipmentType.Dumbbells
            ],
            TrainingLocation.Home =>
            [
                EquipmentType.Bodyweight, EquipmentType.Bands, EquipmentType.Dumbbells,
                EquipmentType.YogaMat, EquipmentType.FoamRoller
            ],
            TrainingLocation.Both => Enum.GetValues<EquipmentType>().ToList(),
            _ => new List<EquipmentType> { EquipmentType.Bodyweight }
        };

        SelectedEquipment.Clear();
        foreach (var eq in suggested)
            SelectedEquipment.Add(eq);

        SyncEquipmentChips();
        SyncEquipment();
    }

    private void SyncEquipment()
    {
        _coordinator.Data.AvailableEquipment = [.. SelectedEquipment];
    }

    [RelayCommand]
    private void IncrementFrequency()
    {
        if (CurrentFrequency < 7)
            CurrentFrequency++;
    }

    [RelayCommand]
    private void DecrementFrequency()
    {
        if (CurrentFrequency > 1)
            CurrentFrequency--;
    }

    [RelayCommand]
    private void IncrementDuration()
    {
        if (SessionDurationMinutes < 120)
            SessionDurationMinutes += 15;
    }

    [RelayCommand]
    private void DecrementDuration()
    {
        if (SessionDurationMinutes > 15)
            SessionDurationMinutes -= 15;
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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        _selectedTrainingLocation = _coordinator.Data.TrainingLocation;
        _selectedExperienceLevel = _coordinator.Data.ExperienceLevel;
        _trainingMonths = _coordinator.Data.TrainingMonths;
        _currentFrequency = _coordinator.Data.CurrentFrequency;
        _sessionDurationMinutes = _coordinator.Data.SessionDurationMinutes;
        _selectedTimeOfDay = _coordinator.Data.PreferredTimeOfDay;

        // Load equipment selections
        foreach (var eq in _coordinator.Data.AvailableEquipment)
            SelectedEquipment.Add(eq);

        // Load day selections
        foreach (var day in _coordinator.Data.AvailableDays)
            SelectedDays.Add(day);
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<ExperienceLevel> ExperienceLevels =>
        [ExperienceLevel.Beginner, ExperienceLevel.Novice, ExperienceLevel.Intermediate, ExperienceLevel.Advanced, ExperienceLevel.Elite];

    public List<TrainingLocation> TrainingLocationOptions { get; } =
        [TrainingLocation.Gym, TrainingLocation.Home, TrainingLocation.Both];

    public List<string> TimeOfDayOptions { get; } = ["Morning", "Afternoon", "Evening"];

    public List<string> AllDays { get; } = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    public List<EquipmentType> AllEquipment { get; } =
        Enum.GetValues<EquipmentType>().ToList();

    [ObservableProperty]
    private TrainingLocation _selectedTrainingLocation;

    [ObservableProperty]
    private ExperienceLevel _selectedExperienceLevel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrainingMonthsDisplay))]
    private int _trainingMonths;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrequencyDisplay))]
    private int _currentFrequency;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationDisplay))]
    private int _sessionDurationMinutes;

    [ObservableProperty]
    private string _selectedTimeOfDay;

    public string TrainingMonthsDisplay => $"{TrainingMonths} months";
    public string FrequencyDisplay => $"{CurrentFrequency} days/week";
    public string DurationDisplay => $"{SessionDurationMinutes} minutes";

    public ObservableCollection<EquipmentType> SelectedEquipment { get; } = [];
    public ObservableCollection<string> SelectedDays { get; } = [];

    partial void OnSelectedTrainingLocationChanged(TrainingLocation value)
    {
        _coordinator.Data.TrainingLocation = value;
        ApplyEquipmentSuggestions(value);
    }

    partial void OnSelectedExperienceLevelChanged(ExperienceLevel value) => _coordinator.Data.ExperienceLevel = value;
    partial void OnTrainingMonthsChanged(int value) => _coordinator.Data.TrainingMonths = value;
    partial void OnCurrentFrequencyChanged(int value) => _coordinator.Data.CurrentFrequency = value;
    partial void OnSessionDurationMinutesChanged(int value) => _coordinator.Data.SessionDurationMinutes = value;
    partial void OnSelectedTimeOfDayChanged(string value) => _coordinator.Data.PreferredTimeOfDay = value;

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

    [RelayCommand]
    private async Task ToggleDayAsync()
    {
        var unselected = AllDays.Where(d => !SelectedDays.Contains(d)).ToArray();
        if (unselected.Length == 0) return;

        var result = await Shell.Current.DisplayActionSheet("Add Training Day", "Cancel", null, unselected);
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        if (!SelectedDays.Contains(result))
            SelectedDays.Add(result);

        SyncDays();
    }

    [RelayCommand]
    private void RemoveDay(string day)
    {
        SelectedDays.Remove(day);
        SyncDays();
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

        SyncEquipment();
    }

    private void SyncEquipment()
    {
        _coordinator.Data.AvailableEquipment = [.. SelectedEquipment];
    }

    private void SyncDays()
    {
        _coordinator.Data.AvailableDays = [.. SelectedDays];
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
    private void IncrementMonths()
    {
        if (TrainingMonths < 360)
            TrainingMonths++;
    }

    [RelayCommand]
    private void DecrementMonths()
    {
        if (TrainingMonths > 0)
            TrainingMonths--;
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

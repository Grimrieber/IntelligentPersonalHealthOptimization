using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Workout;

public partial class WorkingWeightsViewModel : BaseViewModel
{
    private readonly IWorkingWeightService _service;
    private readonly IUserService _userService;
    private int _userId;
    private bool _isInitializing;

    public WorkingWeightsViewModel(IWorkingWeightService service, IUserService userService)
    {
        _service = service;
        _userService = userService;
        Title = "Strength Baselines";
    }

    public ObservableCollection<LiftEntry> Lifts { get; } = [];

    [ObservableProperty] private string _saveStatus = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        _isInitializing = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;
            _userId = user.Id;

            var existing = await _service.GetAllAsync(_userId);
            Lifts.Clear();
            foreach (var benchmark in _service.Benchmarks)
            {
                var match = existing.FirstOrDefault(e => e.LiftKey == benchmark.Key);
                var entry = new LiftEntry(benchmark.Key, benchmark.DisplayName, benchmark.Description,
                                          OnEntryChanged);
                if (match != null)
                {
                    entry.WeightKg = (double)match.WeightKg;
                    entry.IsEstimated = match.IsEstimated;
                }
                Lifts.Add(entry);
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WorkingWeights.Load", ex);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
        }
    }

    private void OnEntryChanged(LiftEntry entry)
    {
        if (_isInitializing) return;
        _ = SaveEntryAsync(entry);
    }

    private async Task SaveEntryAsync(LiftEntry entry)
    {
        try
        {
            if (entry.WeightKg <= 0)
                return;

            var kg = (decimal)entry.WeightKg;
            await _service.SaveAsync(_userId, entry.LiftKey, kg, entry.IsEstimated);
            SaveStatus = $"{entry.DisplayName}: {kg:F1} kg saved";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WorkingWeights.Save", ex);
            SaveStatus = "Could not save — try again.";
        }
    }

    [RelayCommand]
    private async Task DoneAsync() => await Shell.Current.GoToAsync("..");
}

public partial class LiftEntry : ObservableObject
{
    // Slider range / increment. 0 means "not set". 2.5 kg matches standard plate jumps.
    public const double MinKg = 0;
    public const double MaxKg = 250;
    public const double StepKg = 2.5;

    public string LiftKey { get; }
    public string DisplayName { get; }
    public string Description { get; }

    private readonly Action<LiftEntry> _onChanged;

    public LiftEntry(string liftKey, string displayName, string description, Action<LiftEntry> onChanged)
    {
        LiftKey = liftKey;
        DisplayName = displayName;
        Description = description;
        _onChanged = onChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeightKgDisplay))]
    [NotifyPropertyChangedFor(nameof(WeightLbDisplay))]
    private double _weightKg;

    [ObservableProperty]
    private bool _isEstimated;

    public string WeightKgDisplay => WeightKg > 0 ? $"{WeightKg:0.#} kg" : "Not set";

    public string WeightLbDisplay => WeightKg > 0 ? $"≈ {WeightKg * 2.20462:F0} lb" : string.Empty;

    [RelayCommand]
    private void Increase() => WeightKg = Math.Min(MaxKg, WeightKg + StepKg);

    [RelayCommand]
    private void Decrease() => WeightKg = Math.Max(MinKg, WeightKg - StepKg);

    partial void OnWeightKgChanged(double value)
    {
        // Snap continuous slider drags to the nearest 2.5 kg. Re-assigning an already
        // snapped value is a no-op (ObservableProperty only fires on change), so this
        // doesn't recurse.
        var snapped = Math.Round(value / StepKg) * StepKg;
        if (Math.Abs(snapped - value) > 0.0001)
        {
            WeightKg = snapped;
            return;
        }
        _onChanged(this);
    }

    partial void OnIsEstimatedChanged(bool value) => _onChanged(this);
}

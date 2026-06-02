using System.Collections.ObjectModel;
using IntelligentPersonalHealthOptimization.Helpers;

namespace IntelligentPersonalHealthOptimization.Controls;

/// <summary>
/// Reusable Mon–Sun day picker. Tapping a chip selects/deselects a day; the number of
/// selected days IS the weekly training frequency (dual purpose). The selection is exposed
/// as a canonical CSV (Mon→Sun order) via <see cref="SelectedDaysCsv"/> for the host to persist.
/// Enforces <see cref="MinDays"/>/<see cref="MaxDays"/> so the program generator always gets a
/// supported count. Used by onboarding, the Equipment Intel flow, and Settings.
/// </summary>
public partial class TrainingDaySelector : ContentView
{
    private static readonly (string Day, string Short)[] WeekDays =
    [
        ("Monday", "Mon"), ("Tuesday", "Tue"), ("Wednesday", "Wed"),
        ("Thursday", "Thu"), ("Friday", "Fri"), ("Saturday", "Sat"), ("Sunday", "Sun"),
    ];

    // Suppresses the CSV->toggles sync while we are the ones writing the CSV (prevents loops).
    private bool _syncing;

    public ObservableCollection<DayToggle> Days { get; } = [];

    public TrainingDaySelector()
    {
        InitializeComponent();
        foreach (var (day, sh) in WeekDays)
        {
            var toggle = new DayToggle(day, sh);
            // Each chip carries its own command so the tap binds to the item itself.
            toggle.ToggleCommand = new Command(() => OnToggle(toggle));
            Days.Add(toggle);
        }
        UpdateSummary();
    }

    public static readonly BindableProperty SelectedDaysCsvProperty = BindableProperty.Create(
        nameof(SelectedDaysCsv), typeof(string), typeof(TrainingDaySelector),
        string.Empty, BindingMode.TwoWay, propertyChanged: OnSelectedDaysCsvChanged);

    public string SelectedDaysCsv
    {
        get => (string)GetValue(SelectedDaysCsvProperty);
        set => SetValue(SelectedDaysCsvProperty, value);
    }

    public static readonly BindableProperty MinDaysProperty = BindableProperty.Create(
        nameof(MinDays), typeof(int), typeof(TrainingDaySelector), 2);

    public int MinDays
    {
        get => (int)GetValue(MinDaysProperty);
        set => SetValue(MinDaysProperty, value);
    }

    public static readonly BindableProperty MaxDaysProperty = BindableProperty.Create(
        nameof(MaxDays), typeof(int), typeof(TrainingDaySelector), 6);

    public int MaxDays
    {
        get => (int)GetValue(MaxDaysProperty);
        set => SetValue(MaxDaysProperty, value);
    }

    public static readonly BindableProperty StatusProperty = BindableProperty.Create(
        nameof(Status), typeof(string), typeof(TrainingDaySelector), string.Empty);

    /// <summary>Transient validation message (e.g. min/max reached); empty when nothing to say.</summary>
    public string Status
    {
        get => (string)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        private set { _summary = value; OnPropertyChanged(); }
    }

    private static void OnSelectedDaysCsvChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (TrainingDaySelector)bindable;
        if (self._syncing) return;
        self.ApplyCsvToToggles((string?)newValue);
    }

    private void ApplyCsvToToggles(string? csv)
    {
        var set = (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var toggle in Days)
            toggle.IsSelected = set.Contains(toggle.Day);

        UpdateSummary();
    }

    private void OnToggle(DayToggle? day)
    {
        if (day == null) return;

        var selectedCount = Days.Count(d => d.IsSelected);

        if (day.IsSelected)
        {
            if (selectedCount <= MinDays)
            {
                Status = $"Keep at least {MinDays} training days.";
                return;
            }
            day.IsSelected = false;
        }
        else
        {
            if (selectedCount >= MaxDays)
            {
                Status = $"Up to {MaxDays} training days.";
                return;
            }
            day.IsSelected = true;
        }

        Status = string.Empty;
        UpdateSummary();

        // Push the new selection out to the host, in canonical Mon→Sun order.
        _syncing = true;
        SelectedDaysCsv = string.Join(",", Days.Where(d => d.IsSelected).Select(d => d.Day));
        _syncing = false;
    }

    private void UpdateSummary()
    {
        var count = Days.Count(d => d.IsSelected);
        Summary = $"{count} {(count == 1 ? "day" : "days")}/week";
    }
}

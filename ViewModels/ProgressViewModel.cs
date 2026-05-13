using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using Microcharts;
using SkiaSharp;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class ProgressViewModel : BaseViewModel
{
    private readonly IProgressService _progressService;
    private readonly IUserService _userService;

    public ProgressViewModel(IProgressService progressService, IUserService userService)
    {
        _progressService = progressService;
        _userService = userService;
        Title = "Progress";
    }

    [ObservableProperty]
    private Chart? _weightChart;

    [ObservableProperty]
    private List<ProgressHistoryItem> _history = new();

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private string _latestWeight = "--";

    [ObservableProperty]
    private string _weightChange = string.Empty;

    [RelayCommand]
    private async Task LoadProgressAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var entries = await _progressService.GetProgressHistoryAsync(user.Id);
            HasData = entries.Count > 0;

            // Build history list
            var historyItems = new List<ProgressHistoryItem>();
            foreach (var entry in entries)
            {
                var measurements = await _progressService.GetMeasurementsForEntryAsync(entry.Id);
                historyItems.Add(new ProgressHistoryItem
                {
                    Date = entry.EntryDate.ToLocalTime().ToString("MMM dd, yyyy"),
                    Weight = entry.WeightKg.HasValue ? $"{entry.WeightKg:F1} kg" : "--",
                    Notes = entry.Notes,
                    MeasurementSummary = string.Join(", ", measurements.Select(m => $"{m.MeasurementName}: {m.ValueCm:F1}cm"))
                });
            }
            History = historyItems;

            // Latest weight
            if (entries.Count > 0 && entries[0].WeightKg.HasValue)
            {
                LatestWeight = $"{entries[0].WeightKg:F1} kg";

                if (entries.Count >= 2)
                {
                    var prev = entries.Skip(1).FirstOrDefault(e => e.WeightKg.HasValue);
                    if (prev?.WeightKg.HasValue == true)
                    {
                        var diff = entries[0].WeightKg!.Value - prev.WeightKg!.Value;
                        WeightChange = diff >= 0 ? $"+{diff:F1} kg" : $"{diff:F1} kg";
                    }
                }
            }

            // Build weight chart
            var weightTrend = await _progressService.GetWeightTrendAsync(user.Id);
            if (weightTrend.Count >= 2)
            {
                var chartEntries = weightTrend.Select(w => new ChartEntry((float)w.weight)
                {
                    Label = w.date.ToLocalTime().ToString("MM/dd"),
                    ValueLabel = w.weight.ToString("F1"),
                    Color = SKColor.Parse("#512BD4")
                }).ToList();

                WeightChart = new LineChart
                {
                    Entries = chartEntries,
                    LineMode = LineMode.Straight,
                    LineSize = 3,
                    PointMode = PointMode.Circle,
                    PointSize = 12,
                    BackgroundColor = SKColors.Transparent,
                    LabelTextSize = 28
                };
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddProgressAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.AddProgress);
    }
}

public class ProgressHistoryItem
{
    public string Date { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string MeasurementSummary { get; set; } = string.Empty;
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using Microsoft.Maui.ApplicationModel;

namespace IntelligentPersonalHealthOptimization.ViewModels;

[QueryProperty(nameof(DayId), "dayId")]
public partial class WorkoutDayViewModel : BaseViewModel
{
    private readonly IDatabaseService _databaseService;
    private readonly IExerciseMediaService _mediaService;
    private readonly IBundledExerciseMediaService _bundledMedia;
    private readonly IExercisePerformanceService _performanceService;
    private readonly IUserService _userService;

    public WorkoutDayViewModel(
        IDatabaseService databaseService,
        IExerciseMediaService mediaService,
        IBundledExerciseMediaService bundledMedia,
        IExercisePerformanceService performanceService,
        IUserService userService)
    {
        _databaseService = databaseService;
        _mediaService = mediaService;
        _bundledMedia = bundledMedia;
        _performanceService = performanceService;
        _userService = userService;
    }

    [ObservableProperty]
    private int _dayId;

    [ObservableProperty]
    private string _dayName = string.Empty;

    [ObservableProperty]
    private string _focus = string.Empty;

    [ObservableProperty]
    private List<ExerciseSection> _exerciseSections = new();

    partial void OnDayIdChanged(int value)
    {
        if (value > 0)
            _ = LoadDayAsync();
    }

    private async Task LoadDayAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            int userId = user?.Id ?? 0;

            var db = await _databaseService.GetConnectionAsync();

            var day = await db.Table<WorkoutDay>().FirstOrDefaultAsync(d => d.Id == DayId);
            if (day == null) return;

            DayName = day.DayName;
            Focus = day.Focus;
            Title = day.DayName;

            var workoutExercises = await db.Table<WorkoutExercise>()
                .Where(we => we.WorkoutDayId == DayId)
                .OrderBy(we => we.OrderIndex)
                .ToListAsync();

            var allExercises = await db.Table<Exercise>().ToListAsync();
            var exerciseDict = allExercises.ToDictionary(e => e.Id);

            var sections = workoutExercises
                .GroupBy(we => we.Category)
                .OrderBy(g => GetCategoryOrder(g.Key))
                .Select(g => new ExerciseSection
                {
                    CategoryName = FormatCategory(g.Key),
                    // Only Main starts expanded — everything else collapses by default
                    IsExpanded = g.Key == ExerciseCategory.Main,
                    Exercises = g.Select(we =>
                    {
                        exerciseDict.TryGetValue(we.ExerciseId, out var exercise);
                        var name = exercise?.Name ?? "Unknown Exercise";
                        // VideoUrl now only comes from wger live; seeded VideoUrl is ignored here
                        // since wger lookup runs after this in LoadImagesAsync.
                        return new WorkoutExerciseDisplay
                        {
                            WorkoutDayId = DayId,
                            ExerciseId = we.ExerciseId,
                            ExerciseName = name,
                            SetsReps = FormatSetsReps(we),
                            Tempo = we.Tempo,
                            Rest = we.RestSeconds > 0 ? $"{we.RestSeconds}s rest" : "",
                            FormCues = FormatFormCues(exercise?.FormCues),
                            Notes = we.Notes,
                            Description = exercise?.Description ?? "",
                            YouTubeSearchUrl = BuildYouTubeSearchUrl(name),
                            RecommendedWeightDisplay = FormatRecommendedWeight(we.RecommendedWeightKg),
                            PerformanceService = _performanceService,
                            UserId = userId,
                            DefaultSetCount = we.Sets
                        };
                    }).ToList()
                })
                .ToList();

            // Resolve bundled (offline) images synchronously before exposing the sections —
            // these are local file lookups, no network. Saves a flash of "no image" before
            // the wger fetch resolves.
            foreach (var section in sections)
            {
                foreach (var exercise in section.Exercises)
                {
                    var bundled = _bundledMedia.TryGetImage(exercise.ExerciseName);
                    if (bundled != null)
                    {
                        exercise.ImageSource = bundled;
                        exercise.ImageAttribution = _bundledMedia.Attribution;
                        exercise.HasImage = true;
                    }
                }
            }

            ExerciseSections = sections;

            // Fall back to wger in the background for any exercise without a bundled image.
            _ = LoadImagesAsync(sections);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadImagesAsync(List<ExerciseSection> sections)
    {
        try
        {
            // Only fetch wger images for exercises that didn't get a bundled image.
            var needsFetch = sections.SelectMany(s => s.Exercises)
                .Where(e => !e.HasImage)
                .ToList();
            if (needsFetch.Count == 0) return;

            using var semaphore = new SemaphoreSlim(4);
            // Always try wger — even when we already have a bundled GIF — because wger may have
            // a real video we'd rather show than the 2-frame still-loop. Image-only wger results
            // only fill in for exercises with no bundled GIF.
            var all = sections.SelectMany(s => s.Exercises).ToList();
            var tasks = all.Select(async exercise =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var media = await _mediaService.FindByNameAsync(exercise.ExerciseName);
                    if (media == null) return;

                    // YouTube embed is now the primary demo source; wger image/video are no
                    // longer rendered in the workout card UI. Leaving this no-op so the
                    // service stays loaded and warm (any future use can fetch).
                    _ = media;
                }
                finally
                {
                    semaphore.Release();
                }
            });
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WorkoutDay.LoadImages", ex);
        }
    }

    private static int GetCategoryOrder(ExerciseCategory category) => category switch
    {
        ExerciseCategory.Corrective => 0,
        ExerciseCategory.Activation => 1,
        ExerciseCategory.Warmup => 2,
        ExerciseCategory.Main => 3,
        ExerciseCategory.Cooldown => 4,
        ExerciseCategory.Stretch => 5,
        _ => 99
    };

    private static string FormatCategory(ExerciseCategory category) => category switch
    {
        ExerciseCategory.Corrective => "Corrective Exercises",
        ExerciseCategory.Activation => "Activation",
        ExerciseCategory.Warmup => "Warmup",
        ExerciseCategory.Main => "Main Exercises",
        ExerciseCategory.Cooldown => "Cooldown / Stretches",
        ExerciseCategory.Stretch => "Stretches",
        _ => category.ToString()
    };

    private static string FormatSetsReps(WorkoutExercise we)
    {
        if (we.RepsMin == we.RepsMax)
            return $"{we.Sets} x {we.RepsMin}";
        return $"{we.Sets} x {we.RepsMin}-{we.RepsMax}";
    }

    /// <summary>
    /// Form cues are stored as ";"-separated short phrases in seed data.
    /// Render as a bullet list so each cue reads as a distinct coaching point.
    /// </summary>
    private static string FormatFormCues(string? cues)
    {
        if (string.IsNullOrWhiteSpace(cues)) return string.Empty;
        var lines = cues.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Select(c => $"• {c}");
        return string.Join("\n", lines);
    }

    private static string BuildYouTubeSearchUrl(string exerciseName)
    {
        var query = Uri.EscapeDataString($"{exerciseName} form tutorial");
        return $"https://m.youtube.com/results?search_query={query}";
    }

    /// <summary>"70 kg / 154 lb" formatted recommendation, or empty when no recommendation exists.</summary>
    private static string FormatRecommendedWeight(decimal? kg)
    {
        if (!kg.HasValue || kg.Value <= 0) return string.Empty;
        var k = kg.Value;
        var lb = (double)k * 2.20462;
        return $"{k:F1} kg / {lb:F0} lb";
    }
}

public partial class ExerciseSection : ObservableObject
{
    public string CategoryName { get; set; } = string.Empty;
    public List<WorkoutExerciseDisplay> Exercises { get; set; } = new();

    /// <summary>Main exercises expand by default; ancillary sections (warmup/corrective/
    /// cooldown) collapse so the user sees the workout they care about first.</summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    public string ExerciseCountSummary => $"{Exercises.Count} exercise{(Exercises.Count == 1 ? "" : "s")}";

    public string ToggleIcon => IsExpanded ? "▼" : "▶";

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ToggleIcon));

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}

public partial class WorkoutExerciseDisplay : ObservableObject
{
    public int WorkoutDayId { get; set; }
    public int ExerciseId { get; set; }
    public int UserId { get; set; }
    public int DefaultSetCount { get; set; } = 3;
    public IExercisePerformanceService? PerformanceService { get; set; }

    public string ExerciseName { get; set; } = string.Empty;
    public string SetsReps { get; set; } = string.Empty;
    public string Tempo { get; set; } = string.Empty;
    public string Rest { get; set; } = string.Empty;
    public string FormCues { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>"70 kg / 154 lb" style label, or empty if no recommendation available.</summary>
    public string RecommendedWeightDisplay { get; set; } = string.Empty;
    public bool HasRecommendedWeight => !string.IsNullOrWhiteSpace(RecommendedWeightDisplay);

    [ObservableProperty] private ImageSource? _imageSource;
    [ObservableProperty] private string _imageAttribution = string.Empty;
    [ObservableProperty] private bool _hasImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasYouTubeDemo))]
    private string _youTubeSearchUrl = string.Empty;

    public bool HasYouTubeDemo => !string.IsNullOrWhiteSpace(YouTubeSearchUrl);

    public bool HasNoGuide =>
        string.IsNullOrWhiteSpace(Description) && string.IsNullOrWhiteSpace(FormCues);

    [ObservableProperty] private bool _isGuideExpanded;

    [RelayCommand]
    private void ToggleGuide() => IsGuideExpanded = !IsGuideExpanded;

    /// <summary>Per-set weight + reps inputs. Populated lazily when the log section opens.</summary>
    public ObservableCollection<SetLogEntry> SetLogs { get; } = new();

    [ObservableProperty] private bool _isLogExpanded;

    [RelayCommand]
    private async Task ToggleLogAsync()
    {
        IsLogExpanded = !IsLogExpanded;
        if (IsLogExpanded && SetLogs.Count == 0)
            await LoadSetLogsAsync();
    }

    private async Task LoadSetLogsAsync()
    {
        if (PerformanceService == null) return;
        try
        {
            var existing = await PerformanceService.GetForDayAsync(UserId, WorkoutDayId, ExerciseId);
            for (int i = 1; i <= DefaultSetCount; i++)
            {
                var saved = existing.FirstOrDefault(e => e.SetNumber == i);
                SetLogs.Add(new SetLogEntry(
                    i,
                    saved?.WeightKg,
                    saved?.RepsCompleted,
                    OnSetChanged));
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WorkoutExerciseDisplay.LoadSetLogs", ex);
        }
    }

    private async void OnSetChanged(SetLogEntry entry)
    {
        if (PerformanceService == null) return;
        try
        {
            await PerformanceService.SaveSetAsync(UserId, WorkoutDayId, ExerciseId,
                entry.SetNumber, entry.WeightKg, entry.RepsCompleted ?? 0);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WorkoutExerciseDisplay.OnSetChanged", ex);
        }
    }
}

/// <summary>One row in the per-exercise set-log table. Auto-saves on change.</summary>
public partial class SetLogEntry : ObservableObject
{
    public int SetNumber { get; }

    private readonly Action<SetLogEntry> _onChanged;

    public SetLogEntry(int setNumber, decimal? initialKg, int? initialReps, Action<SetLogEntry> onChanged)
    {
        SetNumber = setNumber;
        _onChanged = onChanged;
        if (initialKg.HasValue) _weightKgText = initialKg.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        if (initialReps.HasValue) _repsText = initialReps.Value.ToString();
    }

    [ObservableProperty] private string _weightKgText = string.Empty;
    [ObservableProperty] private string _repsText = string.Empty;

    public decimal? WeightKg =>
        decimal.TryParse(WeightKgText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var w) && w > 0 ? w : null;

    public int? RepsCompleted =>
        int.TryParse(RepsText, out var r) && r > 0 ? r : null;

    public string SetLabel => $"Set {SetNumber}";

    partial void OnWeightKgTextChanged(string value) => _onChanged(this);
    partial void OnRepsTextChanged(string value) => _onChanged(this);
}

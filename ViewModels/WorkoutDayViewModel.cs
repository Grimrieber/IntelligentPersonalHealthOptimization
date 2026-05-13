using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

[QueryProperty(nameof(DayId), "dayId")]
public partial class WorkoutDayViewModel : BaseViewModel
{
    private readonly IDatabaseService _databaseService;

    public WorkoutDayViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
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
                    Exercises = g.Select(we =>
                    {
                        exerciseDict.TryGetValue(we.ExerciseId, out var exercise);
                        return new WorkoutExerciseDisplay
                        {
                            ExerciseName = exercise?.Name ?? "Unknown Exercise",
                            SetsReps = FormatSetsReps(we),
                            Tempo = we.Tempo,
                            Rest = we.RestSeconds > 0 ? $"{we.RestSeconds}s rest" : "",
                            FormCues = exercise?.FormCues?.Replace(";", "\n") ?? "",
                            Notes = we.Notes,
                            Description = exercise?.Description ?? ""
                        };
                    }).ToList()
                })
                .ToList();

            ExerciseSections = sections;
        }
        finally
        {
            IsBusy = false;
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
}

public class ExerciseSection
{
    public string CategoryName { get; set; } = string.Empty;
    public List<WorkoutExerciseDisplay> Exercises { get; set; } = new();
}

public class WorkoutExerciseDisplay
{
    public string ExerciseName { get; set; } = string.Empty;
    public string SetsReps { get; set; } = string.Empty;
    public string Tempo { get; set; } = string.Empty;
    public string Rest { get; set; } = string.Empty;
    public string FormCues { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

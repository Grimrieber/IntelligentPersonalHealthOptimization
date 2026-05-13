using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Implementation;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class WorkoutProgramViewModel : BaseViewModel
{
    private readonly IDatabaseService _databaseService;
    private readonly IUserService _userService;

    public WorkoutProgramViewModel(IDatabaseService databaseService, IUserService userService)
    {
        _databaseService = databaseService;
        _userService = userService;
        Title = "Workout";
    }

    // Assessment status
    [ObservableProperty] private bool _hasAssessment;
    [ObservableProperty] private string _lastAssessmentDate = string.Empty;
    [ObservableProperty] private int _movementScore;
    [ObservableProperty] private string _scoreDescription = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _identifiedSyndromes = new();
    [ObservableProperty] private bool _hasSyndromes;

    // Program
    [ObservableProperty] private string _programName = string.Empty;
    [ObservableProperty] private string _programDetails = string.Empty;
    [ObservableProperty] private bool _hasProgram;
    [ObservableProperty] private List<WorkoutDayItem> _workoutDays = new();

    [RelayCommand]
    private async Task LoadProgramAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var db = await _databaseService.GetConnectionAsync();

            // Load latest CES assessment
            var cesAssessment = await db.Table<Models.CesAssessment>()
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.AssessmentDate)
                .FirstOrDefaultAsync();

            HasAssessment = cesAssessment != null;

            if (cesAssessment != null)
            {
                LastAssessmentDate = cesAssessment.AssessmentDate.ToLocalTime().ToString("MMM dd, yyyy");
                MovementScore = cesAssessment.OverallMovementScore;
                ScoreDescription = CesSyndromeEngine.GetScoreDescription(cesAssessment.OverallMovementScore);

                // Parse syndromes
                IdentifiedSyndromes.Clear();
                if (!string.IsNullOrEmpty(cesAssessment.IdentifiedSyndromes))
                {
                    foreach (var s in cesAssessment.IdentifiedSyndromes.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Enum.TryParse<DistortionSyndrome>(s.Trim(), out var syndrome))
                            IdentifiedSyndromes.Add(CesSyndromeEngine.GetSyndromeName(syndrome));
                    }
                }
                HasSyndromes = IdentifiedSyndromes.Count > 0;
            }

            // Load active workout program
            var program = await db.Table<WorkoutProgram>()
                .Where(p => p.UserId == user.Id && p.IsActive)
                .FirstOrDefaultAsync();

            if (program == null)
            {
                HasProgram = false;
                WorkoutDays = new List<WorkoutDayItem>();
                return;
            }

            HasProgram = true;
            ProgramName = program.ProgramName;
            ProgramDetails = $"{program.Phase} Phase | {program.DaysPerWeek} days/week | {program.DurationWeeks} weeks";

            var days = await db.Table<WorkoutDay>()
                .Where(d => d.WorkoutProgramId == program.Id)
                .OrderBy(d => d.DayNumber)
                .ToListAsync();

            var dayItems = new List<WorkoutDayItem>();
            foreach (var day in days)
            {
                var exerciseCount = await db.Table<WorkoutExercise>()
                    .Where(e => e.WorkoutDayId == day.Id)
                    .CountAsync();

                dayItems.Add(new WorkoutDayItem
                {
                    DayId = day.Id,
                    DayName = day.DayName,
                    Focus = day.Focus,
                    ExerciseCount = exerciseCount
                });
            }
            WorkoutDays = dayItems;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WorkoutProgram.LoadProgram", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenDayAsync(WorkoutDayItem day)
    {
        await Shell.Current.GoToAsync($"{RouteConstants.WorkoutDay}?dayId={day.DayId}");
    }

    [RelayCommand]
    private async Task StartAssessmentAsync()
    {
        await Shell.Current.GoToAsync(RouteConstants.CesIntro);
    }

    [RelayCommand]
    private async Task GenerateProgramAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var db = await _databaseService.GetConnectionAsync();

            // Get latest CES assessment
            var cesAssessment = await db.Table<Models.CesAssessment>()
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.AssessmentDate)
                .FirstOrDefaultAsync();

            if (cesAssessment == null)
            {
                await Shell.Current.DisplayAlert("No Assessment",
                    "Please complete a body posture assessment first.", "OK");
                return;
            }

            // Parse syndromes
            var syndromes = new List<DistortionSyndrome>();
            if (!string.IsNullOrEmpty(cesAssessment.IdentifiedSyndromes))
            {
                foreach (var s in cesAssessment.IdentifiedSyndromes.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Enum.TryParse<DistortionSyndrome>(s.Trim(), out var syndrome))
                        syndromes.Add(syndrome);
                }
            }

            // Collect all target compensations from identified syndromes
            var targetCompensations = syndromes
                .SelectMany(CesSyndromeEngine.GetTargetCompensations)
                .Distinct()
                .ToList();

            // Get all CES exercises from the database
            var allExercises = await db.Table<Exercise>()
                .Where(e => e.IsActive &&
                    (e.Category == ExerciseCategory.Inhibit ||
                     e.Category == ExerciseCategory.Lengthen ||
                     e.Category == ExerciseCategory.Activate ||
                     e.Category == ExerciseCategory.Integrate))
                .ToListAsync();

            // Match exercises to compensations
            var matched = new List<(Exercise exercise, ExerciseCategory phase)>();
            foreach (var exercise in allExercises)
            {
                if (string.IsNullOrEmpty(exercise.CorrectsCompensations)) continue;
                var corrects = exercise.CorrectsCompensations.Split(',');
                if (corrects.Any(c => targetCompensations.Contains(c.Trim())))
                    matched.Add((exercise, exercise.Category));
            }

            if (matched.Count == 0)
            {
                await Shell.Current.DisplayAlert("No Exercises Found",
                    "No corrective exercises matched your assessment results. Your movement looks great!", "OK");
                return;
            }

            // Deactivate any existing program
            var existingPrograms = await db.Table<WorkoutProgram>()
                .Where(p => p.UserId == user.Id && p.IsActive)
                .ToListAsync();
            foreach (var p in existingPrograms)
            {
                p.IsActive = false;
                await db.UpdateAsync(p);
            }

            // Create corrective program
            var program = new WorkoutProgram
            {
                UserId = user.Id,
                AssessmentSessionId = cesAssessment.Id,
                ProgramName = "Corrective Exercise Program",
                Phase = ExercisePhase.Stabilization,
                DaysPerWeek = 4,
                DurationWeeks = 6,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(42)
            };
            await db.InsertAsync(program);

            // Create workout day with exercises in CES Continuum order
            var day = new WorkoutDay
            {
                WorkoutProgramId = program.Id,
                DayNumber = 1,
                DayName = "Corrective Session",
                Focus = string.Join(", ", syndromes.Take(3).Select(CesSyndromeEngine.GetSyndromeName))
            };
            await db.InsertAsync(day);

            var orderIndex = 0;

            // Add exercises in CES Continuum order: Inhibit → Lengthen → Activate → Integrate
            var phases = new[] { ExerciseCategory.Inhibit, ExerciseCategory.Lengthen,
                                 ExerciseCategory.Activate, ExerciseCategory.Integrate };

            foreach (var phase in phases)
            {
                var phaseExercises = matched
                    .Where(m => m.phase == phase)
                    .Select(m => m.exercise)
                    .DistinctBy(e => e.Id)
                    .Take(phase == ExerciseCategory.Inhibit ? 4 :
                          phase == ExerciseCategory.Lengthen ? 4 :
                          phase == ExerciseCategory.Activate ? 5 : 3)
                    .ToList();

                foreach (var exercise in phaseExercises)
                {
                    var (sets, repsMin, repsMax, tempo, rest, notes) = GetExerciseParams(phase);

                    await db.InsertAsync(new WorkoutExercise
                    {
                        WorkoutDayId = day.Id,
                        ExerciseId = exercise.Id,
                        OrderIndex = orderIndex++,
                        Category = exercise.Category,
                        Sets = sets,
                        RepsMin = repsMin,
                        RepsMax = repsMax,
                        Tempo = tempo,
                        RestSeconds = rest,
                        Notes = notes
                    });
                }
            }

            // Update CES assessment with generated program ID
            cesAssessment.GeneratedProgramId = program.Id;
            await db.UpdateAsync(cesAssessment);

            await Shell.Current.DisplayAlert("Program Generated",
                $"Your corrective exercise program has been created with {orderIndex} exercises " +
                "following the Corrective Exercise Continuum:\n\n" +
                "1. Inhibit (foam rolling)\n" +
                "2. Lengthen (stretching)\n" +
                "3. Activate (strengthening)\n" +
                "4. Integrate (functional movement)",
                "View Program");

            // Reload
            await LoadProgramAsync();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WorkoutProgram.GenerateProgram", ex);
            await Shell.Current.DisplayAlert("Error", "Could not generate program. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static (int sets, int repsMin, int repsMax, string tempo, int rest, string notes) GetExerciseParams(ExerciseCategory phase) => phase switch
    {
        ExerciseCategory.Inhibit => (1, 1, 1, "Hold", 0, "Hold tender spots 30-90 sec per side. Deep breaths."),
        ExerciseCategory.Lengthen => (2, 1, 1, "Hold", 0, "Hold 30 sec per side. Exhale to deepen."),
        ExerciseCategory.Activate => (2, 12, 15, "2-2-2-0", 30, "Slow controlled tempo. Feel the target muscle."),
        ExerciseCategory.Integrate => (2, 10, 12, "3-1-2-0", 45, "Focus on proper form throughout."),
        _ => (2, 10, 12, "2-1-2-0", 30, "")
    };
}

public class WorkoutDayItem
{
    public int DayId { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string Focus { get; set; } = string.Empty;
    public int ExerciseCount { get; set; }
    public string ExerciseCountDisplay => $"{ExerciseCount} exercises";
}

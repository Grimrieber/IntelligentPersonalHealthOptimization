using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Workout;

[QueryProperty(nameof(SessionId), "sessionId")]
[QueryProperty(nameof(WasSkipped), "skipped")]
public partial class ProgramPreviewViewModel : BaseViewModel
{
    private readonly IEquipmentIntelService _intelService;
    private readonly IPrescriptionEngine _prescriptionEngine;
    private readonly IUserService _userService;
    private int _userId;

    public ProgramPreviewViewModel(
        IEquipmentIntelService intelService,
        IPrescriptionEngine prescriptionEngine,
        IUserService userService)
    {
        _intelService = intelService;
        _prescriptionEngine = prescriptionEngine;
        _userService = userService;
        Title = "Your Program Preview";
    }

    [ObservableProperty] private int _sessionId;
    [ObservableProperty] private bool _wasSkipped;

    [ObservableProperty] private string _phaseLine = string.Empty;
    [ObservableProperty] private string _daysLine = string.Empty;
    [ObservableProperty] private string _durationLine = string.Empty;
    [ObservableProperty] private string _equipmentLine = string.Empty;
    [ObservableProperty] private string _tailoringLine = string.Empty;

    [ObservableProperty] private bool _showDegradedBanner;
    [ObservableProperty] private bool _programGenerated;
    [ObservableProperty] private string _generatedProgramName = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;
            _userId = user.Id;

            var ctx = await _intelService.LoadContextAsync(_userId);
            var inventory = ctx.ExistingInventory;

            ShowDegradedBanner = WasSkipped || inventory.Count == 0;

            // Phase: use CES score if available, else derive from experience level
            string phase;
            string phaseReason;
            if (ctx.LatestCes != null)
            {
                phase = PhaseFromScore(ctx.LatestCes.OverallMovementScore);
                phaseReason = $"Your movement score ({ctx.LatestCes.OverallMovementScore}) → {phase} phase";
            }
            else
            {
                phase = PhaseFromExperience(ctx.ExperienceLevel);
                phaseReason = $"Your {Humanize(ctx.ExperienceLevel)} experience → {phase} phase (no CES — take it later for a more tailored read)";
            }

            PhaseLine = $"Phase: {phase}";
            DaysLine = $"Days/week: {ctx.DaysPerWeek}";
            DurationLine = $"Session: {ctx.SessionDurationMinutes} min";
            EquipmentLine = inventory.Count > 0
                ? $"Equipment: {inventory.Count} items captured"
                : "Equipment: using onboarding defaults (add detail for better picks)";

            var bullets = new List<string> { phaseReason };
            bullets.Add($"Your {Humanize(ctx.FitnessGoal)} goal");
            if (inventory.Count > 0) bullets.Add($"Your {Humanize(ctx.TrainingLocation)} inventory ({inventory.Count} items)");
            if (ctx.HasKneeIssue) bullets.Add("Knee history — will favor leg press / safer loading");
            if (ctx.HasShoulderIssue) bullets.Add("Shoulder history — will substitute landmine for overhead press where possible");
            if (ctx.HasBackIssue) bullets.Add("Back history — will route around heavy axial loading");

            TailoringLine = string.Join("\n• ", bullets.Prepend("Tailored from:"));
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("ProgramPreview.Load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        // CES is optional — engine handles SessionId == 0 by defaulting from experience level.
        IsBusy = true;
        try
        {
            var program = await _prescriptionEngine.GenerateProgramAsync(_userId, SessionId);
            ProgramGenerated = true;
            GeneratedProgramName = program.ProgramName;
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("ProgramPreview.Generate", ex);
            await Shell.Current.DisplayAlert("Error", $"Could not generate program: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string PhaseFromScore(int score) => score switch
    {
        <= 40 => "Stabilization",
        <= 60 => "Muscular Endurance",
        <= 80 => "Hypertrophy",
        _ => "Strength"
    };

    private static string PhaseFromExperience(ExperienceLevel level) => level switch
    {
        ExperienceLevel.Beginner => "Stabilization",
        ExperienceLevel.Novice => "Muscular Endurance",
        ExperienceLevel.Intermediate => "Hypertrophy",
        ExperienceLevel.Advanced => "Hypertrophy",
        ExperienceLevel.Elite => "Strength",
        _ => "Muscular Endurance"
    };

    private static string Humanize(Enum value) =>
        System.Text.RegularExpressions.Regex.Replace(value.ToString(), @"(?<=[a-z])([A-Z])", " $1");

    [RelayCommand]
    private async Task ViewProgramAsync()
    {
        await Shell.Current.GoToAsync($"//{RouteConstants.Workout}");
    }

    [RelayCommand]
    private async Task BackAsync() => await Shell.Current.GoToAsync("..");
}

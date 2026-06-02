using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules.PrescriptionRules;

public class MainProgramRules : IRuleSet
{
    private readonly List<Exercise> _exerciseLibrary;

    public MainProgramRules(List<Exercise> exerciseLibrary)
    {
        _exerciseLibrary = exerciseLibrary;
    }

    public void Evaluate(RuleContext context, RuleResult result)
    {
        // Determine training phase based on movement score
        result.RecommendedPhase = context.OverallMovementScore switch
        {
            <= 40 => ExercisePhase.Stabilization,
            <= 60 => ExercisePhase.MuscularEndurance,
            <= 80 => ExercisePhase.Hypertrophy,
            _ => ExercisePhase.Strength
        };

        result.DaysPerWeek = context.DaysPerWeek > 0 ? context.DaysPerWeek : AppConstants.DefaultDaysPerWeek;
        result.DurationWeeks = AppConstants.DefaultProgramDurationWeeks;

        // Training level scales volume + difficulty (not days/week — that stays driven
        // by the user's availability). Higher level → more exercises, more sets, and
        // access to harder exercise variations.
        var level = context.TrainingProfile?.ExperienceLevel ?? ExperienceLevel.Beginner;
        int exerciseDelta = LevelExerciseDelta(level);
        int setDelta = LevelSetDelta(level);
        int difficultyDelta = LevelDifficultyDelta(level);

        int phaseMaxDifficulty = result.RecommendedPhase switch
        {
            ExercisePhase.Stabilization => 2,
            ExercisePhase.MuscularEndurance => 3,
            ExercisePhase.Hypertrophy => 4,
            _ => 5
        };
        int maxDifficulty = Math.Clamp(phaseMaxDifficulty + difficultyDelta, 1, 5);

        var daySplits = GetDaySplits(result.DaysPerWeek);
        int targetMainCount = Math.Clamp(
            GetExerciseCountForSession(context.SessionDurationMinutes) + exerciseDelta, 3, 12);
        var usedAcrossWeek = new HashSet<int>(); // track to avoid repeating same exercise in different days

        for (int dayIdx = 0; dayIdx < daySplits.Length; dayIdx++)
        {
            BuildDayMainExercises(
                dayIdx,
                daySplits[dayIdx].muscles,
                targetMainCount,
                maxDifficulty,
                result.RecommendedPhase,
                context.AvailableEquipment,
                usedAcrossWeek,
                setDelta,
                result);
        }

        // Cooldown stretches (kept simple — first 4 cooldown exercises in library)
        var cooldowns = _exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Cooldown)
            .Take(4)
            .ToList();
        int cooldownOrder = 1;
        foreach (var exercise in cooldowns)
        {
            result.CooldownExercises.Add(new ExercisePrescription
            {
                ExerciseId = exercise.Id,
                Sets = 1, RepsMin = 1, RepsMax = 1,
                Tempo = "Hold 30s", RestSeconds = 0,
                OrderIndex = cooldownOrder++,
                Notes = "Hold for 30 seconds per side"
            });
        }
    }

    /// <summary>
    /// Build a real-trainer-style day: 1 primary compound, several accessories, isolations,
    /// and core. Scales total count by session duration. Tries to vary across the week.
    /// </summary>
    private void BuildDayMainExercises(
        int dayIdx,
        MuscleGroup[] dayMuscles,
        int targetCount,
        int maxDifficulty,
        ExercisePhase phase,
        List<string> availableEquipment,
        HashSet<int> usedAcrossWeek,
        int setDelta,
        RuleResult result)
    {
        // First non-Core muscle is the day's "primary" focus
        var primaryMuscle = dayMuscles.FirstOrDefault(m => m != MuscleGroup.Core);
        var accessoryMuscles = dayMuscles
            .Where(m => m != primaryMuscle && m != MuscleGroup.Core)
            .ToList();
        bool dayHasCore = dayMuscles.Contains(MuscleGroup.Core);

        var dayExercises = new List<(Exercise exercise, MovementRole role)>();
        var usedToday = new HashSet<int>();

        // 1. Primary compound for the day's focus muscle (always first)
        var primary = PickExercise(primaryMuscle, MovementRole.Primary, maxDifficulty,
            availableEquipment, usedToday, usedAcrossWeek);
        if (primary != null)
        {
            dayExercises.Add((primary, MovementRole.Primary));
            usedToday.Add(primary.Id);
        }

        // 2. Reserve a core slot if applicable
        int coreReserved = dayHasCore && targetCount >= 4 ? 1 : 0;

        // 3. Fill remaining with accessory + isolation across all day muscles
        int remaining = targetCount - dayExercises.Count - coreReserved;
        // Aim for roughly 60% accessory, 40% isolation of remaining
        int isolationSlots = Math.Max(1, remaining / 3);
        int accessorySlots = remaining - isolationSlots;

        // Accessories: rotate across primary + accessory muscles
        var accessoryRotation = (new[] { primaryMuscle }).Concat(accessoryMuscles).ToList();
        for (int i = 0; i < accessorySlots; i++)
        {
            var muscle = accessoryRotation[i % accessoryRotation.Count];
            var pick = PickExercise(muscle, MovementRole.Accessory, maxDifficulty,
                availableEquipment, usedToday, usedAcrossWeek);
            if (pick != null)
            {
                dayExercises.Add((pick, MovementRole.Accessory));
                usedToday.Add(pick.Id);
            }
        }

        // Isolations: prioritize smaller muscles (tris/bis/calves/shoulders)
        var isolationRotation = accessoryRotation;
        for (int i = 0; i < isolationSlots; i++)
        {
            var muscle = isolationRotation[i % isolationRotation.Count];
            var pick = PickExercise(muscle, MovementRole.Isolation, maxDifficulty,
                availableEquipment, usedToday, usedAcrossWeek);
            if (pick != null)
            {
                dayExercises.Add((pick, MovementRole.Isolation));
                usedToday.Add(pick.Id);
            }
        }

        // 4. Core slot
        if (coreReserved > 0)
        {
            var core = PickExercise(MuscleGroup.Core, MovementRole.Accessory, maxDifficulty,
                availableEquipment, usedToday, usedAcrossWeek);
            if (core != null)
            {
                dayExercises.Add((core, MovementRole.Isolation));
                usedToday.Add(core.Id);
            }
        }

        // Mark exercises as used across the week so other days prefer different lifts
        foreach (var id in usedToday) usedAcrossWeek.Add(id);

        // Emit prescriptions with role-appropriate sets/reps
        int order = 1;
        foreach (var (exercise, role) in dayExercises)
        {
            var (sets, repsMin, repsMax, tempo, rest, notes) = GetRoleParameters(phase, role);
            result.MainExercises.Add(new ExercisePrescription
            {
                ExerciseId = exercise.Id,
                Sets = Math.Max(2, sets + setDelta),
                RepsMin = repsMin,
                RepsMax = repsMax,
                Tempo = tempo,
                RestSeconds = rest,
                OrderIndex = order++,
                DayIndex = dayIdx,
                Notes = notes
            });
        }
    }

    /// <summary>
    /// Picks the best library match for muscle + role + equipment. Loosens criteria stepwise
    /// rather than returning null when an exact match fails.
    /// </summary>
    private Exercise? PickExercise(
        MuscleGroup muscle,
        MovementRole role,
        int maxDifficulty,
        List<string> availableEquipment,
        HashSet<int> usedToday,
        HashSet<int> usedAcrossWeek)
    {
        // Step 1: ideal — matches muscle + role + equipment + not used this week
        var candidates = _exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Main
                && e.PrimaryMuscle == muscle
                && e.DifficultyLevel <= maxDifficulty
                && e.IsActive
                && HasAvailableEquipment(e, availableEquipment)
                && !usedToday.Contains(e.Id)
                && InferRole(e) == role)
            .OrderByDescending(e => e.DifficultyLevel)
            .ToList();

        var pick = candidates.FirstOrDefault(e => !usedAcrossWeek.Contains(e.Id))
                ?? candidates.FirstOrDefault();
        if (pick != null) return pick;

        // Step 2: same role + equipment but allow secondary-muscle matches
        pick = _exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Main
                && e.DifficultyLevel <= maxDifficulty
                && e.IsActive
                && HasAvailableEquipment(e, availableEquipment)
                && !usedToday.Contains(e.Id)
                && InferRole(e) == role
                && (e.PrimaryMuscle == muscle || e.SecondaryMuscles.Contains(muscle.ToString())))
            .OrderByDescending(e => e.DifficultyLevel)
            .FirstOrDefault();
        if (pick != null) return pick;

        // Step 3: relax role — any exercise hitting that muscle with available equipment
        pick = _exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Main
                && e.PrimaryMuscle == muscle
                && e.DifficultyLevel <= maxDifficulty
                && e.IsActive
                && HasAvailableEquipment(e, availableEquipment)
                && !usedToday.Contains(e.Id))
            .OrderByDescending(e => e.DifficultyLevel)
            .FirstOrDefault();
        if (pick != null) return pick;

        // Step 4: last resort — drop equipment filter (we'll show whatever matches the muscle)
        pick = _exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Main
                && e.PrimaryMuscle == muscle
                && e.DifficultyLevel <= maxDifficulty
                && e.IsActive
                && !usedToday.Contains(e.Id))
            .OrderByDescending(e => e.DifficultyLevel)
            .FirstOrDefault();

        return pick;
    }

    /// <summary>
    /// Infers whether an exercise is Primary (heavy compound), Accessory (mid-weight multi-joint),
    /// or Isolation (single-joint, pump work) based on its name. Best-effort until we have
    /// explicit tagging on each Exercise row.
    /// </summary>
    private static MovementRole InferRole(Exercise e)
    {
        var n = e.Name.ToLowerInvariant();

        // Big compounds first
        string[] primaryKeywords =
        {
            "deadlift", "back squat", "front squat", "bench press", "overhead press",
            "barbell row", "pull-up", "chin-up", "clean", "snatch", "hip thrust",
            "romanian deadlift"
        };
        if (primaryKeywords.Any(k => n.Contains(k))) return MovementRole.Primary;

        // Isolations
        string[] isolationKeywords =
        {
            "curl", "fly", "raise", "extension", "pushdown", "kickback", "shrug",
            "calf raise", "leg extension", "leg curl", "concentration", "preacher",
            "pec deck", "cable cross", "reverse fly", "lateral raise", "front raise"
        };
        if (isolationKeywords.Any(k => n.Contains(k)))
        {
            // Hip extension and knee extension are not isolations; let those fall to accessory
            if (n.Contains("hip extension")) return MovementRole.Accessory;
            return MovementRole.Isolation;
        }

        // Everything else is accessory (DB rows, machine press, lunges, dips, etc.)
        return MovementRole.Accessory;
    }

    private static (int sets, int repsMin, int repsMax, string tempo, int rest, string notes)
        GetRoleParameters(ExercisePhase phase, MovementRole role)
    {
        // Phase determines the BASE intensity profile
        var basePrms = phase switch
        {
            ExercisePhase.Stabilization => (sets: 2, repsMin: 12, repsMax: 20, tempo: "4-2-1-0", rest: 60),
            ExercisePhase.MuscularEndurance => (sets: 3, repsMin: 12, repsMax: 15, tempo: "2-0-2-0", rest: 60),
            ExercisePhase.Hypertrophy => (sets: 3, repsMin: 8, repsMax: 12, tempo: "2-0-2-0", rest: 90),
            ExercisePhase.Strength => (sets: 4, repsMin: 4, repsMax: 6, tempo: "2-0-1-0", rest: 120),
            ExercisePhase.Power => (sets: 3, repsMin: 1, repsMax: 5, tempo: "X-0-X-0", rest: 120),
            _ => (sets: 3, repsMin: 10, repsMax: 12, tempo: "2-0-2-0", rest: 60)
        };

        // Role modifies it
        return role switch
        {
            MovementRole.Primary => (
                Math.Max(basePrms.sets + 1, 3),   // extra set for the heavy compound
                basePrms.repsMin, basePrms.repsMax, basePrms.tempo,
                basePrms.rest + 30, "Heavy compound — leave 1-2 reps in reserve"),

            MovementRole.Accessory => (
                basePrms.sets, basePrms.repsMin, basePrms.repsMax, basePrms.tempo,
                basePrms.rest, ""),

            MovementRole.Isolation => (
                Math.Max(basePrms.sets - 1, 2),   // one less set, higher reps for pump
                basePrms.repsMax,
                Math.Min(basePrms.repsMax + 8, 25),
                "2-0-1-0",
                Math.Max(basePrms.rest - 30, 45),
                "Squeeze and control — chase the burn"),

            _ => (basePrms.sets, basePrms.repsMin, basePrms.repsMax, basePrms.tempo, basePrms.rest, "")
        };
    }

    /// <summary>
    /// Maps session duration (minutes) to a target main-exercise count for the day.
    /// Accounts for ~10min warm-up + ~5min cooldown + ~5min per exercise.
    /// </summary>
    private static int GetExerciseCountForSession(int sessionMinutes) => sessionMinutes switch
    {
        <= 20 => 3,
        <= 30 => 4,
        <= 45 => 5,
        <= 60 => 6,
        <= 75 => 7,
        <= 90 => 8,
        _ => 10
    };

    // --- Training-level scaling -------------------------------------------------
    // Extra/fewer exercises per day vs the session-duration baseline. Spread wide
    // enough that each level is clearly distinct above per-day fill variance.
    private static int LevelExerciseDelta(ExperienceLevel level) => level switch
    {
        ExperienceLevel.Beginner => -3,
        ExperienceLevel.Novice => -2,
        ExperienceLevel.Advanced => 2,
        ExperienceLevel.Elite => 4,
        _ => 0, // Intermediate
    };

    // Extra/fewer sets per exercise vs the phase/role baseline (floored at 2 on emit).
    private static int LevelSetDelta(ExperienceLevel level) => level switch
    {
        ExperienceLevel.Beginner => -1,
        ExperienceLevel.Novice => 0,
        ExperienceLevel.Advanced => 1,
        ExperienceLevel.Elite => 2,
        _ => 0, // Intermediate
    };

    // Shift the allowed exercise-difficulty ceiling (1–5) up/down by level.
    private static int LevelDifficultyDelta(ExperienceLevel level) => level switch
    {
        ExperienceLevel.Beginner => -1,
        ExperienceLevel.Advanced => 1,
        ExperienceLevel.Elite => 1,
        _ => 0, // Novice, Intermediate
    };

    private static bool HasAvailableEquipment(Exercise exercise, List<string> availableEquipment)
    {
        if (availableEquipment.Count == 0) return true;
        if (string.IsNullOrEmpty(exercise.Equipment)) return true;

        var userTags = availableEquipment.Select(NormalizeEquipmentTag).ToHashSet();
        var exerciseTags = exercise.Equipment.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeEquipmentTag);

        return exerciseTags.All(eq =>
            userTags.Contains(eq)
            || eq == "bodyweight" || eq == "wall" || eq == "doorway" || eq == "step"
            || eq == "yogamat" || eq == "foamroller");
    }

    /// <summary>
    /// Normalizes equipment tag strings so seed data and engine inventory don't fail to match
    /// over trivial variations like singular/plural or different naming ("Resistance Band" vs "Bands").
    /// </summary>
    private static string NormalizeEquipmentTag(string raw)
    {
        var s = raw.Trim().ToLowerInvariant().Replace(" ", "");
        return s switch
        {
            "resistanceband" or "resistancebands" or "band" => "bands",
            "dumbbell" => "dumbbells",
            "kettlebells" => "kettlebell",
            "pull-upbar" or "pullup-bar" or "pullupbar" => "pullupbar",
            "stabilityball" or "swissball" => "stabilityball",
            "foamroller" => "foamroller",
            "yogamat" or "mat" => "yogamat",
            _ => s
        };
    }

    private static (string name, MuscleGroup[] muscles)[] GetDaySplits(int daysPerWeek)
    {
        return daysPerWeek switch
        {
            2 =>
            [
                ("Upper Body", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.UpperBack, MuscleGroup.Triceps, MuscleGroup.Biceps, MuscleGroup.Core]),
                ("Lower Body", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves, MuscleGroup.Core])
            ],
            4 =>
            [
                ("Upper Push", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps]),
                ("Lower Body", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves]),
                ("Upper Pull", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps]),
                ("Core + Glutes", [MuscleGroup.Core, MuscleGroup.Glutes, MuscleGroup.HipFlexors])
            ],
            5 =>
            [
                ("Chest + Triceps", [MuscleGroup.Chest, MuscleGroup.Triceps]),
                ("Back + Biceps", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps]),
                ("Legs", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Calves]),
                ("Shoulders + Core", [MuscleGroup.Shoulders, MuscleGroup.Core, MuscleGroup.Obliques]),
                ("Glutes + Full Body", [MuscleGroup.Glutes, MuscleGroup.HipFlexors, MuscleGroup.Core])
            ],
            6 =>
            [
                ("Push", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps]),
                ("Pull", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps]),
                ("Legs", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Calves]),
                ("Push + Core", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Core]),
                ("Pull + Core", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Core]),
                ("Legs + Glutes", [MuscleGroup.Quadriceps, MuscleGroup.Glutes, MuscleGroup.Calves])
            ],
            _ =>
            [
                ("Push + Core", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps, MuscleGroup.Core]),
                ("Pull + Core", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps, MuscleGroup.Core]),
                ("Legs + Glutes", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves])
            ]
        };
    }
}

/// <summary>
/// Role within the day — drives sets/reps/rest tuning beyond what phase alone dictates.
/// </summary>
public enum MovementRole
{
    Primary,
    Accessory,
    Isolation
}

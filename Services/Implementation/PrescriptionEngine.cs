using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Rules;
using IntelligentPersonalHealthOptimization.Rules.PostureRules;
using IntelligentPersonalHealthOptimization.Rules.PrescriptionRules;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class PrescriptionEngine : IPrescriptionEngine
{
    private readonly IDatabaseService _databaseService;
    private readonly IWorkingWeightService _workingWeights;

    public PrescriptionEngine(IDatabaseService databaseService, IWorkingWeightService workingWeights)
    {
        _databaseService = databaseService;
        _workingWeights = workingWeights;
    }

    public async Task<WorkoutProgram> GenerateProgramAsync(int userId, int assessmentSessionId)
    {
        var db = await _databaseService.GetConnectionAsync();

        var user = await db.Table<User>().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("User not found");

        // CES / movement assessment is OPTIONAL. If no session id provided (or session not found),
        // we generate a sensible default program from goal + experience. The user gets a working program;
        // if they later complete CES they can regenerate for a more tailored one.
        AssessmentSession? session = null;
        var results = new List<AssessmentResult>();
        FitnessBenchmark? benchmark = null;
        PostureAssessment? postureAssessment = null;

        if (assessmentSessionId > 0)
        {
            session = await db.Table<AssessmentSession>().FirstOrDefaultAsync(s => s.Id == assessmentSessionId);
            if (session != null)
            {
                results = await db.Table<AssessmentResult>()
                    .Where(r => r.AssessmentSessionId == assessmentSessionId).ToListAsync();
                benchmark = await db.Table<FitnessBenchmark>()
                    .FirstOrDefaultAsync(b => b.AssessmentSessionId == assessmentSessionId);
                postureAssessment = await db.Table<PostureAssessment>()
                    .FirstOrDefaultAsync(p => p.AssessmentSessionId == assessmentSessionId);
            }
        }

        var exercises = await db.Table<Exercise>().Where(e => e.IsActive).ToListAsync();

        // Injury flags the user set in Equipment Intel (CSV of "knee"/"shoulder"/"back").
        // Used below to lighten load and add a caution note on exercises that load a flagged area.
        var injuredAreas = ParseInjuredAreas(user.InjuryAreas);
        // When true, exercises that DIRECTLY load a flagged area are dropped instead of lightened.
        var avoidInjured = user.AvoidInjuredExercises;

        // Load expanded data. Use the latest profile row (by Id) so the generator
        // always reads the same row the UI updates, even if duplicate rows exist.
        var trainingProfile = await db.Table<TrainingProfile>()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();

        // Build compensations from movement assessment results (empty if no CES)
        var allCompensations = results
            .Where(r => !string.IsNullOrEmpty(r.DetectedCompensations))
            .SelectMany(r => r.DetectedCompensations.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(s => Enum.Parse<MovementCompensation>(s.Trim()))
            .Distinct()
            .ToList();

        // Add posture-derived compensations
        var postureDeviations = new List<PostureDeviation>();
        if (postureAssessment != null && !string.IsNullOrEmpty(postureAssessment.DetectedDeviations))
        {
            postureDeviations = postureAssessment.DetectedDeviations
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Enum.Parse<PostureDeviation>(s.Trim()))
                .ToList();

            foreach (var deviation in postureDeviations)
            {
                var mapped = PostureAnalysisRules.MapToCompensations(deviation);
                foreach (var comp in mapped)
                {
                    if (!allCompensations.Contains(comp))
                        allCompensations.Add(comp);
                }
            }
        }

        // Parse available equipment — prefer new EquipmentInventoryItem rows; fall back to legacy CSV
        var availableEquipment = new List<string> { "Bodyweight" };
        var inventory = await db.Table<EquipmentInventoryItem>()
            .Where(i => i.UserId == userId)
            .ToListAsync();

        if (inventory.Count > 0)
        {
            availableEquipment = inventory
                .Select(i => MapToLegacyTag(i.ItemType))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();
            if (!availableEquipment.Contains("Bodyweight"))
                availableEquipment.Add("Bodyweight");
        }
        else if (trainingProfile != null && !string.IsNullOrEmpty(trainingProfile.AvailableEquipment))
        {
            availableEquipment = trainingProfile.AvailableEquipment
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();
            if (!availableEquipment.Contains("Bodyweight"))
                availableEquipment.Add("Bodyweight");
        }

        // Determine days per week from training profile
        var daysPerWeek = 3;
        if (trainingProfile != null && !string.IsNullOrEmpty(trainingProfile.AvailableDays))
        {
            var days = trainingProfile.AvailableDays.Split(',', StringSplitOptions.RemoveEmptyEntries);
            daysPerWeek = Math.Clamp(days.Length, 2, 6);
        }

        // When no CES session, derive a sensible movement-score default from the user's GOAL
        // (primary signal) modified by experience level (mild adjustment). Without this,
        // every beginner gets locked into Stabilization regardless of what they want to do —
        // and a beginner who chose "Muscle Building" needs Hypertrophy-phase programming.
        var movementScore = session?.OverallMovementScore
            ?? DefaultMovementScore(trainingProfile?.ExperienceLevel, user.FitnessGoal);

        var context = new RuleContext
        {
            User = user,
            Session = session,
            Results = results,
            AllCompensations = allCompensations,
            OverallMovementScore = movementScore,
            TrainingProfile = trainingProfile,
            FitnessBenchmark = benchmark,
            PostureDeviations = postureDeviations,
            AvailableEquipment = availableEquipment,
            DaysPerWeek = daysPerWeek,
            SessionDurationMinutes = trainingProfile?.SessionDurationMinutes ?? 60
        };

        // Evaluate all rule sets
        var ruleResult = new RuleResult();
        new CorrectiveExerciseRules(exercises).Evaluate(context, ruleResult);
        new WarmupSelectionRules(exercises).Evaluate(context, ruleResult);
        new MainProgramRules(exercises).Evaluate(context, ruleResult);

        // Adjust phase based on fitness benchmarks
        if (benchmark != null)
        {
            ruleResult.RecommendedPhase = BenchmarkScoringRules.AdjustPhaseWithBenchmarks(
                ruleResult.RecommendedPhase, benchmark);
        }

        // Override days per week from training profile
        ruleResult.DaysPerWeek = daysPerWeek;

        // Deactivate previous programs
        var oldPrograms = await db.Table<WorkoutProgram>()
            .Where(p => p.UserId == userId && p.IsActive).ToListAsync();
        foreach (var old in oldPrograms)
        {
            old.IsActive = false;
            await db.UpdateAsync(old);
        }

        // Create new program. AssessmentSessionId is nullable so 0 (no CES) is stored as null.
        var program = new WorkoutProgram
        {
            UserId = userId,
            AssessmentSessionId = assessmentSessionId > 0 ? assessmentSessionId : null,
            ProgramName = $"{ruleResult.RecommendedPhase} Phase Program",
            Phase = ruleResult.RecommendedPhase,
            DaysPerWeek = ruleResult.DaysPerWeek,
            DurationWeeks = ruleResult.DurationWeeks,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(ruleResult.DurationWeeks * 7)
        };
        await db.InsertAsync(program);

        // Build workout days based on available days
        var daySplits = GetDaySplits(ruleResult.DaysPerWeek);

        // Track main exercises used across the program so injury back-fills prefer variety.
        var usedMainAcrossProgram = new HashSet<int>();

        for (int d = 0; d < ruleResult.DaysPerWeek; d++)
        {
            var day = new WorkoutDay
            {
                WorkoutProgramId = program.Id,
                DayNumber = d + 1,
                DayName = $"Day {d + 1} - {daySplits[d].name}",
                Focus = daySplits[d].name
            };
            await db.InsertAsync(day);

            int order = 1;

            // Corrective exercises (same for all days)
            foreach (var ce in ruleResult.CorrectiveExercises)
            {
                await db.InsertAsync(new WorkoutExercise
                {
                    WorkoutDayId = day.Id,
                    ExerciseId = ce.ExerciseId,
                    OrderIndex = order++,
                    Category = ExerciseCategory.Corrective,
                    Sets = ce.Sets,
                    RepsMin = ce.RepsMin,
                    RepsMax = ce.RepsMax,
                    Tempo = ce.Tempo,
                    RestSeconds = ce.RestSeconds,
                    Notes = ce.Notes
                });
            }

            // Day-specific warmup — targets THIS day's muscles, not the same blob every day
            var daySpecificWarmups = PickDayWarmups(exercises, daySplits[d].muscles, count: 4);
            foreach (var we in daySpecificWarmups)
            {
                await db.InsertAsync(new WorkoutExercise
                {
                    WorkoutDayId = day.Id,
                    ExerciseId = we.Id,
                    OrderIndex = order++,
                    Category = ExerciseCategory.Warmup,
                    Sets = 2, RepsMin = 10, RepsMax = 12,
                    Tempo = "2-0-2-0", RestSeconds = 30
                });
            }

            // Day-specific main exercises
            var dayMainExercises = ruleResult.MainExercises
                .Where(me => me.DayIndex == d)
                .OrderBy(me => me.OrderIndex)
                .ToList();

            var usedMainToday = new HashSet<int>();
            var droppedSlots = new List<ExercisePrescription>();

            foreach (var me in dayMainExercises)
            {
                // Look up the exercise name so we can compute a recommended weight
                var exDef = exercises.FirstOrDefault(e => e.Id == me.ExerciseId);

                // Injury-aware adjustment. If "avoid" is on and this exercise directly loads a
                // flagged area, drop it (we back-fill the slot with a joint-safe lift below).
                if (avoidInjured && exDef != null && injuredAreas.Count > 0
                    && DirectlyLoadsInjuredArea(exDef, injuredAreas))
                {
                    droppedSlots.Add(me);
                    continue;
                }

                decimal? recommended = exDef != null
                    ? await _workingWeights.RecommendWeightKgAsync(userId, exDef.Name, me.RepsMin, me.RepsMax)
                    : null;

                // Otherwise, if it broadly loads a flagged area, lighten the recommended load
                // and add a caution note the user will see.
                var injuryNote = string.Empty;
                if (exDef != null && injuredAreas.Count > 0)
                {
                    var flagged = AffectedInjuryAreas(exDef, injuredAreas);
                    if (flagged.Count > 0)
                    {
                        var loadReduced = recommended.HasValue;
                        if (loadReduced)
                            recommended = Math.Round(recommended!.Value * InjuryLoadFactor, 1);
                        injuryNote = BuildInjuryCaution(flagged, loadReduced);
                    }
                }

                usedMainToday.Add(me.ExerciseId);
                usedMainAcrossProgram.Add(me.ExerciseId);
                await db.InsertAsync(new WorkoutExercise
                {
                    WorkoutDayId = day.Id,
                    ExerciseId = me.ExerciseId,
                    OrderIndex = order++,
                    Category = ExerciseCategory.Main,
                    Sets = me.Sets,
                    RepsMin = me.RepsMin,
                    RepsMax = me.RepsMax,
                    Tempo = me.Tempo,
                    RestSeconds = me.RestSeconds,
                    RecommendedWeightKg = recommended,
                    Notes = injuryNote
                });
            }

            // Back-fill each dropped slot with a joint-safe main exercise targeting one of the
            // day's OTHER muscles, so avoiding (say) knee work doesn't leave an empty day.
            if (droppedSlots.Count > 0)
            {
                var avoidMuscles = injuredAreas
                    .SelectMany(a => InjuryDirectLoadMap.TryGetValue(a, out var m) ? m : Array.Empty<MuscleGroup>())
                    .ToHashSet();
                var safeMuscles = daySplits[d].muscles.Where(m => !avoidMuscles.Contains(m)).ToHashSet();

                foreach (var slot in droppedSlots)
                {
                    var sub = exercises
                        .Where(e => e.Category == ExerciseCategory.Main && e.IsActive
                            && safeMuscles.Contains(e.PrimaryMuscle)
                            && !DirectlyLoadsInjuredArea(e, injuredAreas)
                            && !usedMainToday.Contains(e.Id)
                            && MainProgramRules.HasAvailableEquipment(e, availableEquipment))
                        .OrderBy(e => usedMainAcrossProgram.Contains(e.Id) ? 1 : 0)
                        .ThenByDescending(e => e.DifficultyLevel)
                        .FirstOrDefault();
                    if (sub == null) break; // no joint-safe alternative available

                    usedMainToday.Add(sub.Id);
                    usedMainAcrossProgram.Add(sub.Id);

                    decimal? recommended = await _workingWeights.RecommendWeightKgAsync(userId, sub.Name, slot.RepsMin, slot.RepsMax);
                    var injuryNote = string.Empty;
                    var flagged = AffectedInjuryAreas(sub, injuredAreas);
                    if (flagged.Count > 0)
                    {
                        var loadReduced = recommended.HasValue;
                        if (loadReduced)
                            recommended = Math.Round(recommended!.Value * InjuryLoadFactor, 1);
                        injuryNote = BuildInjuryCaution(flagged, loadReduced);
                    }

                    await db.InsertAsync(new WorkoutExercise
                    {
                        WorkoutDayId = day.Id,
                        ExerciseId = sub.Id,
                        OrderIndex = order++,
                        Category = ExerciseCategory.Main,
                        Sets = slot.Sets,
                        RepsMin = slot.RepsMin,
                        RepsMax = slot.RepsMax,
                        Tempo = slot.Tempo,
                        RestSeconds = slot.RestSeconds,
                        RecommendedWeightKg = recommended,
                        Notes = injuryNote
                    });
                }
            }

            // Cooldown exercises (same for all days)
            foreach (var cd in ruleResult.CooldownExercises)
            {
                await db.InsertAsync(new WorkoutExercise
                {
                    WorkoutDayId = day.Id,
                    ExerciseId = cd.ExerciseId,
                    OrderIndex = order++,
                    Category = ExerciseCategory.Cooldown,
                    Sets = cd.Sets,
                    RepsMin = cd.RepsMin,
                    RepsMax = cd.RepsMax,
                    Tempo = cd.Tempo,
                    RestSeconds = cd.RestSeconds,
                    Notes = cd.Notes
                });
            }
        }

        return program;
    }

    // ---- Injury-aware programming -------------------------------------------------
    // Lighten flagged exercises by ~25% and note the caution. "Avoid entirely" can be
    // layered on top of this later (e.g. a severity toggle that excludes instead of lightens).
    private const decimal InjuryLoadFactor = 0.75m;

    // Maps a user-reported injury area to the muscles whose loading we treat as a risk.
    // Broad map — used for the lighten-and-caution path (erring toward cautioning more).
    private static readonly Dictionary<string, (string Label, MuscleGroup[] Muscles)> InjuryMuscleMap = new()
    {
        ["knee"] = ("Knee", new[] { MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves }),
        ["shoulder"] = ("Shoulder", new[] { MuscleGroup.Shoulders, MuscleGroup.RotatorCuff, MuscleGroup.Chest }),
        ["back"] = ("Back", new[] { MuscleGroup.LowerBack, MuscleGroup.ErectorSpinae }),
    };

    // Narrow map — the muscles whose exercises most directly load the injured joint. Used for the
    // "avoid entirely" path so we only drop the prime stressors (e.g. squats for a knee), not
    // knee-safe accessory work like hip thrusts or calf raises.
    private static readonly Dictionary<string, MuscleGroup[]> InjuryDirectLoadMap = new()
    {
        ["knee"] = new[] { MuscleGroup.Quadriceps },
        ["shoulder"] = new[] { MuscleGroup.Shoulders },
        ["back"] = new[] { MuscleGroup.LowerBack, MuscleGroup.ErectorSpinae },
    };

    // True when the exercise's primary or secondary muscles directly load any flagged area.
    private static bool DirectlyLoadsInjuredArea(Exercise ex, List<string> injuredAreas)
    {
        var muscles = new HashSet<MuscleGroup> { ex.PrimaryMuscle };
        foreach (var token in (ex.SecondaryMuscles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<MuscleGroup>(token.Trim(), out var mg))
                muscles.Add(mg);
        }
        return injuredAreas.Any(area =>
            InjuryDirectLoadMap.TryGetValue(area, out var direct) && direct.Any(muscles.Contains));
    }

    private static List<string> ParseInjuredAreas(string? injuryAreas) =>
        (injuryAreas ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(InjuryMuscleMap.ContainsKey)
            .Distinct()
            .ToList();

    // Returns the labels of the flagged areas this exercise loads (primary or secondary muscle).
    private static List<string> AffectedInjuryAreas(Exercise ex, List<string> injuredAreas)
    {
        var muscles = new HashSet<MuscleGroup> { ex.PrimaryMuscle };
        foreach (var token in (ex.SecondaryMuscles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<MuscleGroup>(token.Trim(), out var mg))
                muscles.Add(mg);
        }

        var hits = new List<string>();
        foreach (var area in injuredAreas)
        {
            var (label, areaMuscles) = InjuryMuscleMap[area];
            if (areaMuscles.Any(muscles.Contains))
                hits.Add(label);
        }
        return hits;
    }

    private static string BuildInjuryCaution(List<string> areas, bool loadReduced)
    {
        var which = string.Join(" / ", areas);
        var lead = loadReduced ? "load reduced ~25%" : "keep the load light";
        return $"⚠️ {which} injury flagged — {lead}. Stay pain-free and prioritize control; skip if it aggravates.";
    }

    private static (string name, MuscleGroup[] muscles)[] GetDaySplits(int daysPerWeek)
    {
        return daysPerWeek switch
        {
            2 => [
                ("Upper Body", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.UpperBack, MuscleGroup.Triceps, MuscleGroup.Biceps, MuscleGroup.Core]),
                ("Lower Body", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves, MuscleGroup.Core])
            ],
            3 => [
                ("Push + Core", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps, MuscleGroup.Core]),
                ("Pull + Core", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps, MuscleGroup.Core]),
                ("Legs + Glutes", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves])
            ],
            4 => [
                ("Upper Push", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps]),
                ("Lower Body", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves]),
                ("Upper Pull", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps]),
                ("Core + Glutes", [MuscleGroup.Core, MuscleGroup.Glutes, MuscleGroup.HipFlexors])
            ],
            5 => [
                ("Chest + Triceps", [MuscleGroup.Chest, MuscleGroup.Triceps]),
                ("Back + Biceps", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps]),
                ("Legs", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Calves]),
                ("Shoulders + Core", [MuscleGroup.Shoulders, MuscleGroup.Core, MuscleGroup.Obliques]),
                ("Glutes + Full Body", [MuscleGroup.Glutes, MuscleGroup.HipFlexors, MuscleGroup.Core])
            ],
            6 => [
                ("Push", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps]),
                ("Pull", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps]),
                ("Legs", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Calves]),
                ("Push + Core", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Core]),
                ("Pull + Core", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Core]),
                ("Legs + Glutes", [MuscleGroup.Quadriceps, MuscleGroup.Glutes, MuscleGroup.Calves])
            ],
            _ => [
                ("Push + Core", [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps, MuscleGroup.Core]),
                ("Pull + Core", [MuscleGroup.UpperBack, MuscleGroup.Lats, MuscleGroup.Biceps, MuscleGroup.Core]),
                ("Legs + Glutes", [MuscleGroup.Quadriceps, MuscleGroup.Hamstrings, MuscleGroup.Glutes, MuscleGroup.Calves])
            ]
        };
    }

    /// <summary>
    /// Picks 3-4 warm-up / activation exercises targeting the day's specific muscle groups,
    /// plus 1 general full-body warm-up. Makes each day's prep distinct from others.
    /// </summary>
    private static List<Exercise> PickDayWarmups(
        List<Exercise> library,
        MuscleGroup[] dayMuscles,
        int count)
    {
        var muscleSet = dayMuscles.ToHashSet();
        var picked = new List<Exercise>();
        var usedIds = new HashSet<int>();

        // Prefer activations/warmups whose primary muscle is hit on this day
        var targeted = library
            .Where(e => (e.Category == ExerciseCategory.Activation || e.Category == ExerciseCategory.Warmup)
                && e.IsActive
                && (muscleSet.Contains(e.PrimaryMuscle)
                    || dayMuscles.Any(m => e.SecondaryMuscles.Contains(m.ToString()))))
            .OrderBy(e => e.Category == ExerciseCategory.Activation ? 0 : 1)
            .ToList();

        foreach (var e in targeted)
        {
            if (picked.Count >= count - 1) break;
            if (usedIds.Add(e.Id)) picked.Add(e);
        }

        // Round out with one general warm-up (any warmup-tagged exercise)
        var general = library
            .Where(e => e.Category == ExerciseCategory.Warmup && e.IsActive && !usedIds.Contains(e.Id))
            .FirstOrDefault();
        if (general != null && picked.Count < count)
        {
            picked.Add(general);
            usedIds.Add(general.Id);
        }

        // Fallback: if we still don't have enough, fill with any warmup/activation
        if (picked.Count < count)
        {
            var filler = library
                .Where(e => (e.Category == ExerciseCategory.Activation || e.Category == ExerciseCategory.Warmup)
                    && e.IsActive
                    && !usedIds.Contains(e.Id))
                .Take(count - picked.Count);
            foreach (var f in filler) { picked.Add(f); usedIds.Add(f.Id); }
        }

        return picked;
    }

    /// <summary>
    /// Maps fitness goal + experience level to a "movement score" stand-in that drives the
    /// engine's phase pick when CES isn't taken. Goal is the primary signal — a beginner who
    /// chose Muscle Building should get Hypertrophy programming, not Stabilization.
    /// Experience nudges +/- 10 around the goal target.
    /// </summary>
    private static int DefaultMovementScore(ExperienceLevel? level, FitnessGoal goal)
    {
        int goalBase = goal switch
        {
            FitnessGoal.MuscleBuilding => 70,    // → Hypertrophy (unlocks difficulty 1-4)
            FitnessGoal.WeightLoss => 55,        // → MuscularEndurance
            FitnessGoal.GeneralFitness => 55,    // → MuscularEndurance
            FitnessGoal.Endurance => 55,         // → MuscularEndurance
            FitnessGoal.ImprovedMobility => 35,  // → Stabilization
            FitnessGoal.Rehabilitation => 35,    // → Stabilization
            _ => 55
        };

        int adjust = level switch
        {
            ExperienceLevel.Beginner => -10,
            ExperienceLevel.Novice => -5,
            ExperienceLevel.Intermediate => 0,
            ExperienceLevel.Advanced => 5,
            ExperienceLevel.Elite => 15,
            _ => 0
        };

        // Clamp to valid range that maps cleanly to phases
        return Math.Clamp(goalBase + adjust, 20, 95);
    }

    /// <summary>
    /// Maps a richer EquipmentItemType back to the legacy string tags used by the seed-data
    /// exercise library. Lets the new inventory drive the existing string-match equipment filter
    /// in MainProgramRules without rewriting the rule engine.
    /// </summary>
    private static string MapToLegacyTag(EquipmentItemType item) => item switch
    {
        EquipmentItemType.Bodyweight => "Bodyweight",
        EquipmentItemType.Wall => "Wall",
        EquipmentItemType.Doorway => "Doorway",
        EquipmentItemType.Step => "Step",
        EquipmentItemType.FixedDumbbells => "Dumbbells",
        EquipmentItemType.AdjustableDumbbells => "Dumbbells",
        EquipmentItemType.BarbellOlympic => "Barbell",
        EquipmentItemType.BarbellStandard => "Barbell",
        EquipmentItemType.BarbellFixed => "Barbell",
        EquipmentItemType.Kettlebells => "Kettlebell",
        EquipmentItemType.LoopBands => "Bands",
        EquipmentItemType.TubeBandsHandles => "Bands",
        EquipmentItemType.MiniBands => "Bands",
        EquipmentItemType.PowerRack => "Bench",
        EquipmentItemType.SquatStands => "Bench",
        EquipmentItemType.SmithMachine => "Machine",
        EquipmentItemType.FlatBench => "Bench",
        EquipmentItemType.AdjustableBench => "Bench",
        EquipmentItemType.PullUpBarMounted => "PullUpBar",
        EquipmentItemType.PullUpBarDoorway => "PullUpBar",
        EquipmentItemType.DipStation => "PullUpBar",
        EquipmentItemType.TrxSuspension => "Bands",
        EquipmentItemType.CableColumn => "Cable",
        EquipmentItemType.CableCrossover => "Cable",
        EquipmentItemType.LatPulldown => "Machine",
        EquipmentItemType.SeatedRowMachine => "Machine",
        EquipmentItemType.LegPress => "Machine",
        EquipmentItemType.HackSquat => "Machine",
        EquipmentItemType.LegCurl => "Machine",
        EquipmentItemType.LegExtension => "Machine",
        EquipmentItemType.ChestPressMachine => "Machine",
        EquipmentItemType.ShoulderPressMachine => "Machine",
        EquipmentItemType.PecDeck => "Machine",
        EquipmentItemType.HipThrustMachine => "Machine",
        EquipmentItemType.CalfRaiseMachine => "Machine",
        EquipmentItemType.HyperextensionGhd => "Machine",
        EquipmentItemType.FoamRoller => "Foam Roller",
        EquipmentItemType.YogaMat => "YogaMat",
        EquipmentItemType.StabilityBall => "StabilityBall",
        _ => string.Empty
    };

    public List<Exercise> SelectCorrectiveExercises(List<MovementCompensation> compensations, List<Exercise> exerciseLibrary)
    {
        return exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Corrective
                && compensations.Any(c => e.CorrectsCompensations.Contains(c.ToString())))
            .ToList();
    }

    public List<Exercise> SelectWarmupExercises(List<MovementCompensation> compensations, List<Exercise> exerciseLibrary)
    {
        return exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Activation || e.Category == ExerciseCategory.Warmup)
            .ToList();
    }

    public List<Exercise> SelectMainExercises(ExercisePhase phase, ActivityLevel level, List<Exercise> exerciseLibrary)
    {
        int maxDifficulty = phase switch
        {
            ExercisePhase.Stabilization => 2,
            ExercisePhase.MuscularEndurance => 3,
            _ => 4
        };

        return exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Main && e.DifficultyLevel <= maxDifficulty)
            .ToList();
    }

    public List<Exercise> SelectCooldownExercises(List<MovementCompensation> compensations, List<Exercise> exerciseLibrary)
    {
        return exerciseLibrary
            .Where(e => e.Category == ExerciseCategory.Cooldown)
            .ToList();
    }
}

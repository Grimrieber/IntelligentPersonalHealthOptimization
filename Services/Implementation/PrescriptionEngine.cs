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

    public PrescriptionEngine(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<WorkoutProgram> GenerateProgramAsync(int userId, int assessmentSessionId)
    {
        var db = await _databaseService.GetConnectionAsync();

        var user = await db.Table<User>().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("User not found");
        var session = await db.Table<AssessmentSession>().FirstOrDefaultAsync(s => s.Id == assessmentSessionId)
            ?? throw new InvalidOperationException("Assessment session not found");
        var results = await db.Table<AssessmentResult>()
            .Where(r => r.AssessmentSessionId == assessmentSessionId).ToListAsync();
        var exercises = await db.Table<Exercise>().Where(e => e.IsActive).ToListAsync();

        // Load expanded data
        var trainingProfile = await db.Table<TrainingProfile>()
            .FirstOrDefaultAsync(t => t.UserId == userId);
        var benchmark = await db.Table<FitnessBenchmark>()
            .FirstOrDefaultAsync(b => b.AssessmentSessionId == assessmentSessionId);
        var postureAssessment = await db.Table<PostureAssessment>()
            .FirstOrDefaultAsync(p => p.AssessmentSessionId == assessmentSessionId);

        // Build compensations from movement assessment results
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

        // Parse available equipment
        var availableEquipment = new List<string> { "Bodyweight" };
        if (trainingProfile != null && !string.IsNullOrEmpty(trainingProfile.AvailableEquipment))
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

        var context = new RuleContext
        {
            User = user,
            Session = session,
            Results = results,
            AllCompensations = allCompensations,
            OverallMovementScore = session.OverallMovementScore,
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

        // Create new program
        var program = new WorkoutProgram
        {
            UserId = userId,
            AssessmentSessionId = assessmentSessionId,
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

            // Warmup exercises (same for all days)
            foreach (var we in ruleResult.WarmupExercises)
            {
                await db.InsertAsync(new WorkoutExercise
                {
                    WorkoutDayId = day.Id,
                    ExerciseId = we.ExerciseId,
                    OrderIndex = order++,
                    Category = ExerciseCategory.Warmup,
                    Sets = we.Sets,
                    RepsMin = we.RepsMin,
                    RepsMax = we.RepsMax,
                    Tempo = we.Tempo,
                    RestSeconds = we.RestSeconds
                });
            }

            // Day-specific main exercises
            var dayMainExercises = ruleResult.MainExercises
                .Where(me => me.DayIndex == d)
                .OrderBy(me => me.OrderIndex);

            foreach (var me in dayMainExercises)
            {
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
                    RestSeconds = me.RestSeconds
                });
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

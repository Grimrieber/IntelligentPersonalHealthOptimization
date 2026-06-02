using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class WorkingWeightService : IWorkingWeightService
{
    private readonly IDatabaseService _databaseService;

    public WorkingWeightService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public IReadOnlyList<BenchmarkLift> Benchmarks { get; } = new List<BenchmarkLift>
    {
        new("bench-press",    "Bench Press",    "Barbell flat bench, top heavy single you can do with good form"),
        new("back-squat",     "Back Squat",     "Barbell back squat, parallel or below"),
        new("deadlift",       "Deadlift",       "Conventional deadlift, plates settled at top"),
        new("overhead-press", "Overhead Press", "Standing barbell strict press, no leg drive"),
        new("barbell-row",    "Barbell Row",    "Bent-over barbell row, bar to lower chest"),
    };

    public async Task<List<WorkingWeight>> GetAllAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<WorkingWeight>().Where(w => w.UserId == userId).ToListAsync();
    }

    public async Task<WorkingWeight?> GetByKeyAsync(int userId, string liftKey)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<WorkingWeight>()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.LiftKey == liftKey);
    }

    public async Task SaveAsync(int userId, string liftKey, decimal weightKg, bool isEstimated)
    {
        var db = await _databaseService.GetConnectionAsync();
        var existing = await db.Table<WorkingWeight>()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.LiftKey == liftKey);

        if (existing == null)
        {
            await db.InsertAsync(new WorkingWeight
            {
                UserId = userId, LiftKey = liftKey,
                WeightKg = weightKg, IsEstimated = isEstimated,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.WeightKg = weightKg;
            existing.IsEstimated = isEstimated;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.UpdateAsync(existing);
        }
    }

    public async Task<decimal?> RecommendWeightKgAsync(int userId, string exerciseName, int repsMin, int repsMax)
    {
        var (liftKey, multiplier) = MapExerciseToLift(exerciseName);
        if (liftKey == null) return null;

        var max = await GetByKeyAsync(userId, liftKey);
        if (max == null || max.WeightKg <= 0) return null;

        // Rep-range midpoint drives the %1RM
        var midReps = (repsMin + repsMax) / 2.0;
        var percentOfMax = PercentOfMaxForReps(midReps);

        var recommended = max.WeightKg * (decimal)percentOfMax * (decimal)multiplier;
        // Round to nearest 2.5 kg
        return Math.Round(recommended / 2.5m, MidpointRounding.AwayFromZero) * 2.5m;
    }

    /// <summary>
    /// Maps an exercise's display name to a benchmark lift key + multiplier (e.g. DB variants
    /// of bench are ~40% of barbell max per hand). Returns (null, 0) for unmapped exercises.
    /// </summary>
    private static (string? liftKey, double multiplier) MapExerciseToLift(string exerciseName)
    {
        var n = exerciseName.ToLowerInvariant();

        // BENCH PRESS family
        if (n.Contains("barbell bench") || n == "bench press") return ("bench-press", 1.0);
        if (n.Contains("dumbbell bench")) return ("bench-press", 0.40);
        if (n.Contains("machine") && n.Contains("chest press")) return ("bench-press", 0.85);
        if (n.Contains("cable chest press") || n.Contains("band chest press")) return ("bench-press", 0.60);
        if (n.Contains("incline") && n.Contains("press")) return ("bench-press", 0.85);

        // SQUAT family
        if (n.Contains("barbell back squat") || n == "back squat" || n == "squat") return ("back-squat", 1.0);
        if (n.Contains("front squat")) return ("back-squat", 0.85);
        if (n.Contains("goblet squat")) return ("back-squat", 0.40);
        if (n.Contains("dumbbell squat")) return ("back-squat", 0.50);
        if (n.Contains("leg press")) return ("back-squat", 1.5);   // leg press tolerates more weight
        if (n.Contains("hack squat")) return ("back-squat", 0.90);

        // DEADLIFT family
        if (n.Contains("barbell deadlift") || n == "deadlift") return ("deadlift", 1.0);
        if (n.Contains("romanian deadlift") || n.Contains("rdl")) return ("deadlift", 0.65);
        if (n.Contains("single-leg") && n.Contains("deadlift")) return ("deadlift", 0.35);
        if (n.Contains("hip thrust")) return ("deadlift", 1.0);    // hip thrusts handle ~deadlift weight
        if (n.Contains("hip hinge")) return ("deadlift", 0.60);

        // OVERHEAD PRESS family
        if (n.Contains("barbell overhead") || n == "overhead press") return ("overhead-press", 1.0);
        if (n.Contains("dumbbell shoulder press") || n.Contains("dumbbell overhead")) return ("overhead-press", 0.40);
        if (n.Contains("machine shoulder press")) return ("overhead-press", 0.85);
        if (n.Contains("band shoulder press")) return ("overhead-press", 0.45);

        // ROW family
        if (n.Contains("barbell row") || n.Contains("bent over row")) return ("barbell-row", 1.0);
        if (n.Contains("dumbbell row")) return ("barbell-row", 0.40);
        if (n.Contains("cable row") || n.Contains("seated row")) return ("barbell-row", 0.85);
        if (n.Contains("lat pulldown") || n.Contains("pulldown")) return ("barbell-row", 0.80);

        // Unknown — engine will leave RecommendedWeightKg null
        return (null, 0);
    }

    /// <summary>
    /// Standard %1RM-vs-reps lookup (Epley-ish derivation). Inputs outside the table are clamped.
    /// </summary>
    private static double PercentOfMaxForReps(double reps)
    {
        // Snippets from Prilepin / NSCA references
        if (reps <= 1) return 1.00;
        if (reps <= 3) return 0.93;
        if (reps <= 5) return 0.87;
        if (reps <= 7) return 0.80;
        if (reps <= 10) return 0.75;
        if (reps <= 12) return 0.70;
        if (reps <= 15) return 0.65;
        if (reps <= 20) return 0.58;
        return 0.50;
    }
}

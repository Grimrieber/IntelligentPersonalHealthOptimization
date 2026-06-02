using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IWorkingWeightService
{
    /// <summary>Canonical lift keys with display labels (in display order).</summary>
    IReadOnlyList<BenchmarkLift> Benchmarks { get; }

    Task<List<WorkingWeight>> GetAllAsync(int userId);
    Task<WorkingWeight?> GetByKeyAsync(int userId, string liftKey);
    Task SaveAsync(int userId, string liftKey, decimal weightKg, bool isEstimated);

    /// <summary>
    /// Returns a kg recommendation for the given exercise + rep range, or null when
    /// no working max maps to this exercise (e.g. bodyweight, unknown accessory).
    /// </summary>
    Task<decimal?> RecommendWeightKgAsync(int userId, string exerciseName, int repsMin, int repsMax);
}

public record BenchmarkLift(string Key, string DisplayName, string Description);

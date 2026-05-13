using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules.PrescriptionRules;

public static class BenchmarkScoringRules
{
    /// <summary>
    /// Adjusts the recommended phase based on fitness benchmarks.
    /// Movement score determines the baseline phase; benchmarks can shift it up or down by one level.
    /// </summary>
    public static ExercisePhase AdjustPhaseWithBenchmarks(ExercisePhase movementBasedPhase, FitnessBenchmark? benchmark)
    {
        if (benchmark == null) return movementBasedPhase;

        var benchmarkScore = ScoreBenchmarks(benchmark);

        // If benchmarks are significantly better than movement score suggests, upgrade one phase
        if (benchmarkScore >= 7 && movementBasedPhase < ExercisePhase.Strength)
            return movementBasedPhase + 1;

        // If benchmarks are significantly worse, downgrade one phase
        if (benchmarkScore <= 2 && movementBasedPhase > ExercisePhase.Stabilization)
            return movementBasedPhase - 1;

        return movementBasedPhase;
    }

    /// <summary>
    /// Scores fitness benchmarks on a 0-9 scale.
    /// </summary>
    public static int ScoreBenchmarks(FitnessBenchmark benchmark)
    {
        var score = 0;

        // Push-up scoring (0-3)
        score += benchmark.PushUpCount switch
        {
            >= 30 => 3,
            >= 15 => 2,
            >= 5 => 1,
            _ => 0
        };

        // Plank scoring (0-3)
        score += benchmark.PlankHoldSeconds switch
        {
            >= 120 => 3,
            >= 60 => 2,
            >= 30 => 1,
            _ => 0
        };

        // Squat scoring (0-3)
        score += benchmark.SquatCount switch
        {
            >= 30 => 3,
            >= 15 => 2,
            >= 5 => 1,
            _ => 0
        };

        return score;
    }

    /// <summary>
    /// Determines overall fitness level string from benchmark data.
    /// </summary>
    public static string GetFitnessLevel(FitnessBenchmark benchmark)
    {
        var score = ScoreBenchmarks(benchmark);
        return score switch
        {
            >= 7 => "Advanced",
            >= 4 => "Intermediate",
            _ => "Beginner"
        };
    }

    /// <summary>
    /// Gets age-adjusted push-up norms for comparison display.
    /// </summary>
    public static (int poor, int fair, int good, int excellent) GetPushUpNorms(int age, Gender gender)
    {
        if (gender == Gender.Male)
        {
            return age switch
            {
                < 30 => (15, 20, 30, 40),
                < 40 => (12, 17, 25, 35),
                < 50 => (10, 15, 20, 30),
                _ => (8, 12, 15, 25)
            };
        }
        return age switch
        {
            < 30 => (10, 15, 22, 30),
            < 40 => (8, 12, 18, 25),
            < 50 => (6, 10, 15, 20),
            _ => (4, 8, 12, 18)
        };
    }

    /// <summary>
    /// Gets plank hold norms for comparison display.
    /// </summary>
    public static (int poor, int fair, int good, int excellent) GetPlankNorms()
    {
        return (15, 30, 60, 120);
    }
}

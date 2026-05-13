using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IProgressService
{
    Task<ProgressEntry> LogProgressAsync(int userId, double? weightKg,
        List<(string name, double valueCm)> measurements, string notes);
    Task<List<ProgressEntry>> GetProgressHistoryAsync(int userId, int limit = 30);
    Task<List<BodyMeasurement>> GetMeasurementsForEntryAsync(int progressEntryId);
    Task<ProgressEntry?> GetLatestEntryAsync(int userId);
    Task<List<(DateTime date, double weight)>> GetWeightTrendAsync(int userId, int days = 90);
}

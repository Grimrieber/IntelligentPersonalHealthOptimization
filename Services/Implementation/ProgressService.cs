using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class ProgressService : IProgressService
{
    private readonly IDatabaseService _databaseService;

    public ProgressService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<ProgressEntry> LogProgressAsync(int userId, double? weightKg,
        List<(string name, double valueCm)> measurements, string notes)
    {
        var entry = new ProgressEntry
        {
            UserId = userId,
            EntryDate = DateTime.UtcNow,
            WeightKg = weightKg,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
        await _databaseService.InsertAsync(entry);

        foreach (var (name, valueCm) in measurements)
        {
            if (valueCm > 0)
            {
                await _databaseService.InsertAsync(new BodyMeasurement
                {
                    ProgressEntryId = entry.Id,
                    MeasurementName = name,
                    ValueCm = valueCm
                });
            }
        }

        return entry;
    }

    public async Task<List<ProgressEntry>> GetProgressHistoryAsync(int userId, int limit = 30)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<ProgressEntry>()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.EntryDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<BodyMeasurement>> GetMeasurementsForEntryAsync(int progressEntryId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<BodyMeasurement>()
            .Where(m => m.ProgressEntryId == progressEntryId)
            .ToListAsync();
    }

    public async Task<ProgressEntry?> GetLatestEntryAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<ProgressEntry>()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.EntryDate)
            .FirstOrDefaultAsync();
    }

    public async Task<List<(DateTime date, double weight)>> GetWeightTrendAsync(int userId, int days = 90)
    {
        var db = await _databaseService.GetConnectionAsync();
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var entries = await db.Table<ProgressEntry>()
            .Where(p => p.UserId == userId && p.WeightKg != null && p.EntryDate >= cutoff)
            .OrderBy(p => p.EntryDate)
            .ToListAsync();

        return entries
            .Where(e => e.WeightKg.HasValue)
            .Select(e => (e.EntryDate, e.WeightKg!.Value))
            .ToList();
    }
}

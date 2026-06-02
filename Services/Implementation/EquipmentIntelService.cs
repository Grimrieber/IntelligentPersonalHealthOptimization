using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class EquipmentIntelService : IEquipmentIntelService
{
    private readonly IDatabaseService _databaseService;

    public EquipmentIntelService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<EquipmentIntelContext> LoadContextAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();

        var user = await db.FindAsync<User>(userId);
        var trainingProfile = await db.Table<TrainingProfile>()
            .FirstOrDefaultAsync(t => t.UserId == userId);
        var latestCes = await db.Table<CesAssessment>()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.AssessmentDate)
            .FirstOrDefaultAsync();
        var existingInventory = await db.Table<EquipmentInventoryItem>()
            .Where(i => i.UserId == userId)
            .ToListAsync();
        var existingEnvironment = await db.Table<TrainingEnvironment>()
            .FirstOrDefaultAsync(e => e.UserId == userId);
        var chainTemplates = await db.Table<GymChainEquipmentTemplate>()
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        var context = new EquipmentIntelContext
        {
            UserId = userId,
            User = user,
            TrainingProfile = trainingProfile,
            LatestCes = latestCes,
            TrainingLocation = trainingProfile?.TrainingLocation ?? TrainingLocation.Gym,
            FitnessGoal = user?.FitnessGoal ?? FitnessGoal.GeneralFitness,
            ExperienceLevel = trainingProfile?.ExperienceLevel ?? ExperienceLevel.Beginner,
            SessionDurationMinutes = trainingProfile?.SessionDurationMinutes ?? 60,
            DaysPerWeek = ParseDaysCount(trainingProfile?.AvailableDays),
            LegacyEquipmentMapped = MapLegacyEquipment(trainingProfile?.AvailableEquipment),
            ExistingInventory = existingInventory,
            ExistingEnvironment = existingEnvironment,
            ChainTemplates = chainTemplates,
            HasKneeIssue = ContainsAny(user?.InjuryAreas, "knee"),
            HasShoulderIssue = ContainsAny(user?.InjuryAreas, "shoulder"),
            HasBackIssue = ContainsAny(user?.InjuryAreas, "back", "lumbar", "spine")
        };

        return context;
    }

    public async Task<List<EquipmentInventoryItem>> GetInventoryAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<EquipmentInventoryItem>()
            .Where(i => i.UserId == userId)
            .ToListAsync();
    }

    public async Task SaveInventoryAsync(int userId, List<EquipmentInventoryItem> items)
    {
        var db = await _databaseService.GetConnectionAsync();

        var existing = await db.Table<EquipmentInventoryItem>()
            .Where(i => i.UserId == userId)
            .ToListAsync();
        foreach (var old in existing)
            await db.DeleteAsync(old);

        foreach (var item in items)
        {
            item.Id = 0;
            item.UserId = userId;
            item.CreatedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;
            await db.InsertAsync(item);
        }
    }

    public async Task<TrainingEnvironment?> GetEnvironmentAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<TrainingEnvironment>().FirstOrDefaultAsync(e => e.UserId == userId);
    }

    public async Task SaveEnvironmentAsync(TrainingEnvironment environment)
    {
        var db = await _databaseService.GetConnectionAsync();

        var existing = await db.Table<TrainingEnvironment>()
            .FirstOrDefaultAsync(e => e.UserId == environment.UserId);

        environment.UpdatedAt = DateTime.UtcNow;

        if (existing == null)
        {
            environment.Id = 0;
            environment.CreatedAt = DateTime.UtcNow;
            await db.InsertAsync(environment);
        }
        else
        {
            environment.Id = existing.Id;
            environment.CreatedAt = existing.CreatedAt;
            await db.UpdateAsync(environment);
        }
    }

    public async Task<List<GymChainEquipmentTemplate>> GetChainTemplatesAsync()
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<GymChainEquipmentTemplate>().OrderBy(t => t.SortOrder).ToListAsync();
    }

    public async Task<GymChainEquipmentTemplate?> GetChainTemplateByNameAsync(string chainName)
    {
        if (string.IsNullOrWhiteSpace(chainName)) return null;
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<GymChainEquipmentTemplate>()
            .FirstOrDefaultAsync(t => t.ChainName == chainName);
    }

    public async Task<List<EquipmentItemType>> ParseLegacyEquipmentAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        var profile = await db.Table<TrainingProfile>().FirstOrDefaultAsync(t => t.UserId == userId);
        return MapLegacyEquipment(profile?.AvailableEquipment);
    }

    public bool HasCompletedIntelAsync(int userId, List<EquipmentInventoryItem> currentInventory)
    {
        return currentInventory.Count > 0;
    }

    private static List<EquipmentItemType> MapLegacyEquipment(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [EquipmentItemType.Bodyweight];

        var result = new List<EquipmentItemType>();
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim());

        foreach (var part in parts)
        {
            if (!Enum.TryParse<EquipmentType>(part, true, out var legacy))
                continue;

            var mapped = legacy switch
            {
                EquipmentType.Bodyweight => EquipmentItemType.Bodyweight,
                EquipmentType.Dumbbells => EquipmentItemType.FixedDumbbells,
                EquipmentType.Barbell => EquipmentItemType.BarbellOlympic,
                EquipmentType.Bands => EquipmentItemType.LoopBands,
                EquipmentType.Kettlebell => EquipmentItemType.Kettlebells,
                EquipmentType.Cable => EquipmentItemType.CableColumn,
                EquipmentType.Machine => EquipmentItemType.ChestPressMachine,
                EquipmentType.PullUpBar => EquipmentItemType.PullUpBarMounted,
                EquipmentType.Bench => EquipmentItemType.FlatBench,
                EquipmentType.FoamRoller => EquipmentItemType.FoamRoller,
                EquipmentType.YogaMat => EquipmentItemType.YogaMat,
                EquipmentType.StabilityBall => EquipmentItemType.StabilityBall,
                _ => (EquipmentItemType?)null
            };

            if (mapped.HasValue && !result.Contains(mapped.Value))
                result.Add(mapped.Value);
        }

        if (!result.Contains(EquipmentItemType.Bodyweight))
            result.Add(EquipmentItemType.Bodyweight);

        return result;
    }

    private static int ParseDaysCount(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return 3;
        var days = csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Clamp(days, 2, 6);
    }

    private static bool ContainsAny(string? source, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        foreach (var n in needles)
        {
            if (source.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

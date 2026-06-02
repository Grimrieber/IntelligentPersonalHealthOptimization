using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IEquipmentIntelService
{
    Task<EquipmentIntelContext> LoadContextAsync(int userId);

    Task<List<EquipmentInventoryItem>> GetInventoryAsync(int userId);

    Task SaveInventoryAsync(int userId, List<EquipmentInventoryItem> items);

    Task<TrainingEnvironment?> GetEnvironmentAsync(int userId);

    Task SaveEnvironmentAsync(TrainingEnvironment environment);

    Task<List<GymChainEquipmentTemplate>> GetChainTemplatesAsync();

    Task<GymChainEquipmentTemplate?> GetChainTemplateByNameAsync(string chainName);

    Task<List<EquipmentItemType>> ParseLegacyEquipmentAsync(int userId);

    bool HasCompletedIntelAsync(int userId, List<EquipmentInventoryItem> currentInventory);
}

public class EquipmentIntelContext
{
    public int UserId { get; set; }

    public TrainingProfile? TrainingProfile { get; set; }
    public User? User { get; set; }
    public CesAssessment? LatestCes { get; set; }

    public TrainingLocation TrainingLocation { get; set; }
    public FitnessGoal FitnessGoal { get; set; }
    public ExperienceLevel ExperienceLevel { get; set; }

    public List<EquipmentItemType> LegacyEquipmentMapped { get; set; } = [];
    public List<EquipmentInventoryItem> ExistingInventory { get; set; } = [];
    public TrainingEnvironment? ExistingEnvironment { get; set; }

    public bool HasKneeIssue { get; set; }
    public bool HasShoulderIssue { get; set; }
    public bool HasBackIssue { get; set; }

    public int SessionDurationMinutes { get; set; }
    public int DaysPerWeek { get; set; }

    public List<GymChainEquipmentTemplate> ChainTemplates { get; set; } = [];
}

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

/// <summary>
/// Fetches exercise demonstration imagery from external open-source databases.
/// Currently backed by wger.de (CC-BY-SA 4.0).
/// </summary>
public interface IExerciseMediaService
{
    Task<ExerciseMedia?> FindByNameAsync(string exerciseName, CancellationToken ct = default);
}

public class ExerciseMedia
{
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string Attribution { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
}

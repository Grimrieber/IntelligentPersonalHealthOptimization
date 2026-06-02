namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

/// <summary>
/// Returns demonstration images that are bundled directly in the app package
/// (Resources/Raw/exercise_demos/). Content is MIT-licensed from Free Exercise DB.
/// Works fully offline.
/// </summary>
public interface IBundledExerciseMediaService
{
    /// <summary>
    /// Returns an ImageSource for the bundled image matching the exercise name, or null
    /// when no bundled image exists for that name.
    /// </summary>
    ImageSource? TryGetImage(string exerciseName);

    /// <summary>
    /// Returns the attribution string to display alongside any image returned by this service.
    /// </summary>
    string Attribution { get; }
}

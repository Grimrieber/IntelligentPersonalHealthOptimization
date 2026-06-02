using CommunityToolkit.Mvvm.ComponentModel;

namespace IntelligentPersonalHealthOptimization.ViewModels.Workout;

[QueryProperty(nameof(Url), "url")]
[QueryProperty(nameof(ExerciseName), "name")]
public partial class ExerciseVideoViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _exerciseName = string.Empty;

    partial void OnExerciseNameChanged(string value)
    {
        // Shell uses Title for the navbar; keep it descriptive.
        Title = string.IsNullOrWhiteSpace(value) ? "Exercise video" : value;
    }
}

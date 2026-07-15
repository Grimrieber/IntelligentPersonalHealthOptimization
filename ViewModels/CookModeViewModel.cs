using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

/// <summary>
/// Distraction-free, step-at-a-time cooking view: shows one direction at a time
/// with next/previous and a progress bar, so the phone can sit on the counter.
/// </summary>
[QueryProperty(nameof(RecipeId), "recipeId")]
public partial class CookModeViewModel : BaseViewModel
{
    private readonly IRecipeService _recipeService;

    public CookModeViewModel(IRecipeService recipeService)
    {
        _recipeService = recipeService;
        Title = "Cook Mode";
    }

    [ObservableProperty]
    private int _recipeId = -1;

    [ObservableProperty]
    private string _recipeName = string.Empty;

    private List<string> _steps = new();

    [ObservableProperty]
    private string _currentInstruction = string.Empty;

    [ObservableProperty]
    private string _stepLabel = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _hasSteps;

    [ObservableProperty]
    private bool _canGoPrevious;

    [ObservableProperty]
    private bool _isLastStep;

    private int _index;

    partial void OnRecipeIdChanged(int value)
    {
        if (value >= 0)
            _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var detail = await _recipeService.GetRecipeDetailAsync(RecipeId);
            RecipeName = detail?.Recipe?.RecipeName ?? "Recipe";
            Title = RecipeName;
            _steps = detail?.Directions?
                .OrderBy(d => d.StepNumber)
                .Select(d => d.Instruction ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList() ?? new List<string>();
            HasSteps = _steps.Count > 0;
            ShowStep(0);
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("CookMode load", ex);
            HasSteps = false;
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (_index < _steps.Count - 1)
            ShowStep(_index + 1);
    }

    [RelayCommand]
    private void Previous()
    {
        if (_index > 0)
            ShowStep(_index - 1);
    }

    [RelayCommand]
    private async Task FinishAsync() => await Shell.Current.GoToAsync("..");

    private void ShowStep(int i)
    {
        if (_steps.Count == 0)
        {
            CurrentInstruction = "This recipe has no directions.";
            StepLabel = string.Empty;
            Progress = 0;
            CanGoPrevious = false;
            IsLastStep = true;
            return;
        }
        _index = Math.Clamp(i, 0, _steps.Count - 1);
        CurrentInstruction = _steps[_index];
        StepLabel = $"Step {_index + 1} of {_steps.Count}";
        Progress = (double)(_index + 1) / _steps.Count;
        CanGoPrevious = _index > 0;
        IsLastStep = _index == _steps.Count - 1;
    }
}

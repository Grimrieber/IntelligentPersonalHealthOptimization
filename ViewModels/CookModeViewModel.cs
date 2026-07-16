using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
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
    private readonly IUserService _userService;
    private readonly IDatabaseService _databaseService;

    public CookModeViewModel(IRecipeService recipeService, IUserService userService, IDatabaseService databaseService)
    {
        _recipeService = recipeService;
        _userService = userService;
        _databaseService = databaseService;
        Title = "Cook Mode";
    }

    // Recipe nutrition captured on load, for the "Log to Today" finish action.
    private int? _cal;
    private double _protein, _carbs, _fat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLogButton))]
    private bool _hasNutrition;

    /// <summary>Show the "Log to Today" CTA only on the final step and when we have nutrition.</summary>
    public bool ShowLogButton => IsLastStep && HasNutrition;

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
    [NotifyPropertyChangedFor(nameof(ShowLogButton))]
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

            var n = detail?.Nutrition;
            if (n != null)
            {
                _cal = n.CaloriesPerServing;
                _protein = (double?)n.ProteinGrams ?? 0;
                _carbs = (double?)n.TotalCarbsGrams ?? 0;
                _fat = (double?)n.TotalFatGrams ?? 0;
                HasNutrition = _cal is > 0;
            }
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

    /// <summary>You just cooked it — log it to today's food log in one step.</summary>
    [RelayCommand]
    private async Task LogToTodayAsync()
    {
        if (_cal is not int cal || cal <= 0) { await Shell.Current.GoToAsync(".."); return; }

        var choice = await Shell.Current.DisplayActionSheet(
            "Log to today as…", "Cancel", null, "Breakfast", "Lunch", "Dinner", "Snack");
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var mealType = choice switch
        {
            "Breakfast" => MealType.Breakfast,
            "Lunch" => MealType.Lunch,
            "Dinner" => MealType.Dinner,
            _ => MealType.AfternoonSnack,
        };

        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;

            var entry = new FoodLogEntry
            {
                UserId = user.Id,
                LogDate = DateTime.Today,
                MealType = mealType,
                SavedRecipeId = RecipeId,
                Calories = cal,
                ProteinG = _protein,
                CarbsG = _carbs,
                FatG = _fat,
                Notes = RecipeName,
            };
            await _databaseService.InsertAsync(entry);

            await Shell.Current.DisplayAlert("Logged", $"{RecipeName} added to today.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not log: {ex.Message}", "OK");
        }
    }

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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.WellnessCheckIn;

public partial class WellnessScoffViewModel : BaseViewModel
{
    private readonly IWellnessCheckInCoordinator _coordinator;

    public WellnessScoffViewModel(IWellnessCheckInCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = "How You Feel About Food";
    }

    [ObservableProperty] private string _stepIndicator = string.Empty;
    [ObservableProperty] private double _progressPercentage;

    // SCOFF Questions (-1=unanswered, 0=No, 1=Yes)
    [ObservableProperty] private int _scoffSickIndex = -1;
    [ObservableProperty] private int _scoffControlIndex = -1;
    [ObservableProperty] private int _scoffWeightLossIndex = -1;
    [ObservableProperty] private int _scoffBodyImageIndex = -1;
    [ObservableProperty] private int _scoffFoodDominatesIndex = -1;

    // Nutrition Context
    [ObservableProperty] private int _mealsPerDayIndex;        // 0=1-2, 1=3, 2=4+
    [ObservableProperty] private int _weighFrequencyIndex;     // 0=Never, 1=Weekly, 2=Daily, 3=Multiple
    [ObservableProperty] private int _postMealFeelingIndex;    // 0=Content, 1=Full, 2=Guilty, 3=Compensate
    [ObservableProperty] private int _foodRelationshipIndex;   // 0-4
    [ObservableProperty] private int _foodAvoidanceIndex;      // 0-4

    public string[] YesNoOptions { get; } = ["No", "Yes"];
    public string[] MealsPerDayOptions { get; } = ["1-2 meals", "3 meals", "4+ meals"];
    public string[] WeighFrequencyOptions { get; } = ["Never", "Weekly", "Daily", "Multiple times per day"];
    public string[] PostMealFeelingOptions { get; } = ["Satisfied and content", "Uncomfortably full but it's fine", "Guilty or anxious", "I try to compensate (exercise more, eat less later)"];
    public string[] FoodRelationshipOptions { get; } =
    [
        "Healthy and balanced",
        "Generally good with occasional concerns",
        "Complicated - I think about food a lot",
        "Stressful - food causes me anxiety",
        "I struggle with food regularly"
    ];
    public string[] FoodAvoidanceOptions { get; } =
    [
        "No, I eat most foods",
        "Yes, due to allergies or medical reasons",
        "Yes, due to religious or ethical reasons",
        "Yes, I avoid foods that make me feel guilty",
        "Yes, I'm afraid certain foods will cause weight gain"
    ];

    [RelayCommand]
    private Task LoadAsync()
    {
        var data = _coordinator.Data;
        // Only restore answers if they were actually given before; otherwise leave
        // the pickers unanswered (-1) so the user must respond.
        if (data.ScoffAnswered)
        {
            ScoffSickIndex = data.ScoffSick ? 1 : 0;
            ScoffControlIndex = data.ScoffControl ? 1 : 0;
            ScoffWeightLossIndex = data.ScoffWeightLoss ? 1 : 0;
            ScoffBodyImageIndex = data.ScoffBodyImage ? 1 : 0;
            ScoffFoodDominatesIndex = data.ScoffFoodDominates ? 1 : 0;
        }
        MealsPerDayIndex = data.MealsPerDay switch { <= 2 => 0, 3 => 1, _ => 2 };
        WeighFrequencyIndex = data.WeighFrequency;
        PostMealFeelingIndex = data.PostMealFeeling;
        FoodRelationshipIndex = data.FoodRelationshipFlags;
        FoodAvoidanceIndex = data.FoodAvoidanceFlags;

        StepIndicator = $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";
        ProgressPercentage = _coordinator.ProgressPercentage;
        return Task.CompletedTask;
    }

    private bool AllScoffAnswered =>
        ScoffSickIndex >= 0 && ScoffControlIndex >= 0 && ScoffWeightLossIndex >= 0 &&
        ScoffBodyImageIndex >= 0 && ScoffFoodDominatesIndex >= 0;

    private void SyncToCoordinator()
    {
        var data = _coordinator.Data;
        data.ScoffAnswered = AllScoffAnswered;
        data.ScoffSick = ScoffSickIndex == 1;
        data.ScoffControl = ScoffControlIndex == 1;
        data.ScoffWeightLoss = ScoffWeightLossIndex == 1;
        data.ScoffBodyImage = ScoffBodyImageIndex == 1;
        data.ScoffFoodDominates = ScoffFoodDominatesIndex == 1;
        data.MealsPerDay = MealsPerDayIndex switch { 0 => 2, 1 => 3, _ => 4 };
        data.WeighFrequency = WeighFrequencyIndex;
        data.PostMealFeeling = PostMealFeelingIndex;
        data.FoodRelationshipFlags = FoodRelationshipIndex;
        data.FoodAvoidanceFlags = FoodAvoidanceIndex;
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        if (!AllScoffAnswered)
        {
            await Shell.Current.DisplayAlert("Please answer all questions",
                "These questions help us support you properly — please respond to each one before continuing.",
                "OK");
            return;
        }

        SyncToCoordinator();
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task GoPreviousAsync()
    {
        SyncToCoordinator();
        await _coordinator.GoPreviousAsync();
    }
}

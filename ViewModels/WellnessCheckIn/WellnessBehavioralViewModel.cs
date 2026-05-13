using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.WellnessCheckIn;

public partial class WellnessBehavioralViewModel : BaseViewModel
{
    private readonly IWellnessCheckInCoordinator _coordinator;

    public WellnessBehavioralViewModel(IWellnessCheckInCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = "Your Patterns & Habits";
    }

    [ObservableProperty] private string _stepIndicator = string.Empty;
    [ObservableProperty] private double _progressPercentage;

    // Eating Behaviors
    [ObservableProperty] private bool _bingeEating;
    [ObservableProperty] private bool _restrictToControl;
    [ObservableProperty] private bool _guiltAfterEating;
    [ObservableProperty] private bool _earnFoodExercise;
    [ObservableProperty] private bool _skipMeals;
    [ObservableProperty] private bool _eatInSecret;
    [ObservableProperty] private bool _rigidFoodRules;
    [ObservableProperty] private bool _useLaxativesDietPills;

    // Mindset & Body Image
    [ObservableProperty] private bool _labelFoodsGoodBad;
    [ObservableProperty] private bool _moodAffectedByFood;
    [ObservableProperty] private bool _preoccupiedWithFood;
    [ObservableProperty] private bool _compareBody;
    [ObservableProperty] private bool _avoidSocialFood;
    [ObservableProperty] private bool _anxiousWithoutTracking;
    [ObservableProperty] private bool _worthTiedToWeight;

    // Physical Indicators
    [ObservableProperty] private bool _frequentFatigue;
    [ObservableProperty] private bool _rapidWeightChange;
    [ObservableProperty] private bool _hormonalIssues;
    [ObservableProperty] private bool _dentalThroatIssues;
    [ObservableProperty] private bool _compulsiveExercise;
    [ObservableProperty] private bool _coldOrHairLoss;
    [ObservableProperty] private bool _fainting;

    // Diet count
    [ObservableProperty] private int _dietCountIndex;
    public string[] DietCountOptions { get; } = ["Never", "1-2 diets", "3-5 diets", "6+ diets", "I'm always dieting"];

    [RelayCommand]
    private Task LoadAsync()
    {
        var data = _coordinator.Data;
        BingeEating = data.BingeEating;
        RestrictToControl = data.RestrictToControl;
        GuiltAfterEating = data.GuiltAfterEating;
        EarnFoodExercise = data.EarnFoodExercise;
        SkipMeals = data.SkipMeals;
        EatInSecret = data.EatInSecret;
        RigidFoodRules = data.RigidFoodRules;
        UseLaxativesDietPills = data.UseLaxativesDietPills;

        LabelFoodsGoodBad = data.LabelFoodsGoodBad;
        MoodAffectedByFood = data.MoodAffectedByFood;
        PreoccupiedWithFood = data.PreoccupiedWithFood;
        CompareBody = data.CompareBody;
        AvoidSocialFood = data.AvoidSocialFood;
        AnxiousWithoutTracking = data.AnxiousWithoutTracking;
        WorthTiedToWeight = data.WorthTiedToWeight;

        FrequentFatigue = data.FrequentFatigue;
        RapidWeightChange = data.RapidWeightChange;
        HormonalIssues = data.HormonalIssues;
        DentalThroatIssues = data.DentalThroatIssues;
        CompulsiveExercise = data.CompulsiveExercise;
        ColdOrHairLoss = data.ColdOrHairLoss;
        Fainting = data.Fainting;

        DietCountIndex = data.DietCount;

        StepIndicator = $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";
        ProgressPercentage = _coordinator.ProgressPercentage;
        return Task.CompletedTask;
    }

    private void SyncToCoordinator()
    {
        var data = _coordinator.Data;
        data.BingeEating = BingeEating;
        data.RestrictToControl = RestrictToControl;
        data.GuiltAfterEating = GuiltAfterEating;
        data.EarnFoodExercise = EarnFoodExercise;
        data.SkipMeals = SkipMeals;
        data.EatInSecret = EatInSecret;
        data.RigidFoodRules = RigidFoodRules;
        data.UseLaxativesDietPills = UseLaxativesDietPills;

        data.LabelFoodsGoodBad = LabelFoodsGoodBad;
        data.MoodAffectedByFood = MoodAffectedByFood;
        data.PreoccupiedWithFood = PreoccupiedWithFood;
        data.CompareBody = CompareBody;
        data.AvoidSocialFood = AvoidSocialFood;
        data.AnxiousWithoutTracking = AnxiousWithoutTracking;
        data.WorthTiedToWeight = WorthTiedToWeight;

        data.FrequentFatigue = FrequentFatigue;
        data.RapidWeightChange = RapidWeightChange;
        data.HormonalIssues = HormonalIssues;
        data.DentalThroatIssues = DentalThroatIssues;
        data.CompulsiveExercise = CompulsiveExercise;
        data.ColdOrHairLoss = ColdOrHairLoss;
        data.Fainting = Fainting;

        data.DietCount = DietCountIndex;
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
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

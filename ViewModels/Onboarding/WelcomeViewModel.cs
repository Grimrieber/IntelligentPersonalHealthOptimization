using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class WelcomeViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    public WelcomeViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public string AppDescription =>
        "Welcome to your personal health optimization journey. " +
        "This wizard will guide you through a comprehensive setup to create " +
        "a fully personalized fitness, nutrition, and wellness plan tailored just for you.";

    public List<string> WizardSteps =>
    [
        "1. Welcome - Get started",
        "2. Personal Info - Your basic details",
        "3. Body Metrics - Height and weight",
        "4. Health Screening - Medical conditions and injuries",
        "5. Training Background - Experience and equipment",
        "6. Movement Assessment - Posture and mobility check",
        "7. Fitness Benchmarks - Test your current fitness",
        "8. Goals - Define what you want to achieve",
        "9. Nutrition - Dietary preferences and habits",
        "10. Security - Set your PIN and security question",
        "11. Review - Confirm and create your plan"
    ];

    [RelayCommand]
    private async Task GetStartedAsync()
    {
        await _coordinator.GoNextAsync();
    }
}

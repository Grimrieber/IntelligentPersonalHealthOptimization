using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.WellnessCheckIn;

public partial class WellnessResultViewModel : BaseViewModel
{
    private readonly IWellnessCheckInCoordinator _coordinator;
    private readonly IUserService _userService;

    public WellnessResultViewModel(IWellnessCheckInCoordinator coordinator, IUserService userService)
    {
        _coordinator = coordinator;
        _userService = userService;
        Title = "Your Results";
    }

    [ObservableProperty] private string _stepIndicator = string.Empty;
    [ObservableProperty] private double _progressPercentage;

    // Result display
    [ObservableProperty] private string _resultHeading = string.Empty;
    [ObservableProperty] private string _resultMessage = string.Empty;
    [ObservableProperty] private string _resultIcon = string.Empty;
    [ObservableProperty] private Color _resultColor = Colors.Green;
    [ObservableProperty] private bool _showReferralResources;
    [ObservableProperty] private bool _showCrisisResources;
    [ObservableProperty] private bool _showModifiedNote;
    [ObservableProperty] private string _featuresNote = string.Empty;

    [RelayCommand]
    private async Task LoadResultsAsync()
    {
        IsBusy = true;
        try
        {
            StepIndicator = $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";
            ProgressPercentage = _coordinator.ProgressPercentage;

            var data = _coordinator.Data;
            var user = await _userService.GetCurrentUserAsync();
            var age = user != null
                ? DateTime.UtcNow.Year - user.DateOfBirth.Year
                : 30;

            var riskLevel = data.CalculateRiskLevel(age);

            switch (riskLevel)
            {
                case 1: // Low
                    ResultHeading = "You're all set!";
                    ResultMessage = "Based on your responses, your relationship with food and nutrition looks healthy. " +
                        "We've personalized your Nutrition Coach experience to match your goals.\n\n" +
                        "Remember, it's completely normal to have occasional concerns about food — " +
                        "what matters is that these thoughts don't dominate your life.";
                    ResultColor = Color.FromArgb("#4CAF50");
                    ShowReferralResources = false;
                    ShowCrisisResources = false;
                    ShowModifiedNote = false;
                    FeaturesNote = string.Empty;
                    break;

                case 2: // Moderate
                    ResultHeading = "Your personalized approach is ready";
                    ResultMessage = "Thanks for sharing — your honesty helps us support you better.\n\n" +
                        "We've set up your Nutrition Coach with a flexible, habit-focused approach. " +
                        "Rather than strict calorie counting, we'll focus on building consistent, " +
                        "balanced eating patterns and finding foods you genuinely enjoy.\n\n" +
                        "Many people find it helpful to connect with a registered dietitian " +
                        "for personalized guidance alongside app-based tools.";
                    ResultColor = Color.FromArgb("#FF9800");
                    ShowReferralResources = true;
                    ShowCrisisResources = false;
                    ShowModifiedNote = true;
                    FeaturesNote = "We've adjusted your experience to focus on habits and balance rather than strict tracking.";
                    break;

                case 3: // High
                    ResultHeading = "We want to make sure you get the best support";
                    ResultMessage = "Thank you for being honest with us — that takes courage.\n\n" +
                        "Based on your responses, we think you'd benefit most from working with a " +
                        "healthcare professional who specializes in nutrition and eating behaviors. " +
                        "They can provide personalized support that goes beyond what an app can offer.\n\n" +
                        "This isn't about anything being 'wrong' with you — it's about making sure " +
                        "you have the right team in your corner.";
                    ResultColor = Color.FromArgb("#F44336");
                    ShowReferralResources = true;
                    ShowCrisisResources = false;
                    ShowModifiedNote = true;
                    FeaturesNote = "You can still use the Nutrition Coach for healthy recipes, meal ideas, and nutrition education. " +
                        "Calorie deficit targets and weight loss tracking have been paused. " +
                        "These can be unlocked with a note from your healthcare provider.";
                    break;

                default: // 4 = Critical
                    ResultHeading = "We care about your wellbeing";
                    ResultMessage = "Your responses tell us that you might be going through something " +
                        "that deserves more support than an app can provide.\n\n" +
                        "You are not alone, and there is no shame in asking for help.";
                    ResultColor = Color.FromArgb("#D32F2F");
                    ShowReferralResources = true;
                    ShowCrisisResources = true;
                    ShowModifiedNote = true;
                    FeaturesNote = "We'll keep the Nutrition Coach available for healthy recipes and nutrition education — " +
                        "no tracking, no numbers, just support.";
                    break;
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WellnessResult load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        IsBusy = true;
        try
        {
            await _coordinator.CompleteCheckInAsync();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("WellnessResult complete", ex);
            await Shell.Current.DisplayAlert("Error", "Could not save your check-in. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoPreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }

    [RelayCommand]
    private static async Task OpenNedaAsync()
    {
        await Launcher.OpenAsync("https://www.nationaleatingdisorders.org");
    }

    [RelayCommand]
    private static async Task CallNedaAsync()
    {
        await Launcher.OpenAsync("tel:1-800-931-2237");
    }

    [RelayCommand]
    private static async Task CallCrisisLineAsync()
    {
        await Launcher.OpenAsync("tel:988");
    }

    [RelayCommand]
    private static async Task FindTherapistAsync()
    {
        await Launcher.OpenAsync("https://www.psychologytoday.com/us/therapists/eating-disorders");
    }
}

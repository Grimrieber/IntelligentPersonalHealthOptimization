using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class SecuritySetupViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    public SecuritySetupViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        _pin = _coordinator.Data.Pin;
        _confirmPin = _coordinator.Data.ConfirmPin;
        _selectedSecurityQuestion = _coordinator.Data.SecurityQuestion;
        _securityAnswer = _coordinator.Data.SecurityAnswer;
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<string> SecurityQuestions { get; } =
    [
        "What was the name of your first pet?",
        "What city were you born in?",
        "What is your mother's maiden name?",
        "What was the name of your first school?",
        "What is your favorite food?",
        "What street did you grow up on?"
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinLengthDisplay))]
    private string _pin = string.Empty;

    [ObservableProperty]
    private string _confirmPin = string.Empty;

    [ObservableProperty]
    private string _selectedSecurityQuestion = string.Empty;

    [ObservableProperty]
    private string _securityAnswer = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isPinMatching;

    [ObservableProperty]
    private bool _showPinMismatch;

    public string PinLengthDisplay => $"{Pin?.Length ?? 0}/6 digits";

    partial void OnPinChanged(string value)
    {
        _coordinator.Data.Pin = value ?? string.Empty;
        ValidatePinMatch();
    }

    partial void OnConfirmPinChanged(string value)
    {
        _coordinator.Data.ConfirmPin = value ?? string.Empty;
        ValidatePinMatch();
    }

    partial void OnSelectedSecurityQuestionChanged(string value)
    {
        _coordinator.Data.SecurityQuestion = value ?? string.Empty;
    }

    partial void OnSecurityAnswerChanged(string value)
    {
        _coordinator.Data.SecurityAnswer = value ?? string.Empty;
    }

    private void ValidatePinMatch()
    {
        if (string.IsNullOrEmpty(ConfirmPin))
        {
            IsPinMatching = false;
            ShowPinMismatch = false;
            return;
        }

        IsPinMatching = Pin == ConfirmPin;
        ShowPinMismatch = !IsPinMatching;
    }

    private bool Validate()
    {
        if (string.IsNullOrEmpty(Pin) || Pin.Length < 4 || Pin.Length > 6)
        {
            ValidationMessage = "PIN must be 4-6 digits.";
            return false;
        }

        if (!Pin.All(char.IsDigit))
        {
            ValidationMessage = "PIN must contain only digits.";
            return false;
        }

        if (Pin != ConfirmPin)
        {
            ValidationMessage = "PINs do not match.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SelectedSecurityQuestion))
        {
            ValidationMessage = "Please select a security question.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SecurityAnswer))
        {
            ValidationMessage = "Please provide a security answer.";
            return false;
        }

        ValidationMessage = string.Empty;
        return true;
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (!Validate()) return;
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Helpers;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly ISecurityService _securityService;
    private readonly IUserService _userService;

    public LoginViewModel(ISecurityService securityService, IUserService userService)
    {
        _securityService = securityService;
        _userService = userService;
        Title = "Login";
    }

    [ObservableProperty]
    private string _pin = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBiometricAvailable;

    [ObservableProperty]
    private bool _isResettingPin;

    [ObservableProperty]
    private string _securityQuestion = string.Empty;

    [ObservableProperty]
    private string _securityAnswer = string.Empty;

    [ObservableProperty]
    private string _newPin = string.Empty;

    [ObservableProperty]
    private string _confirmNewPin = string.Empty;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        try
        {
            // Check session FIRST (instant, no DB call) — handles app resume from background
            if (App.HasValidSession)
            {
                App.TouchSession();
                await Shell.Current.GoToAsync($"//{RouteConstants.Dashboard}");
                return;
            }

            var hasAccount = await _userService.HasUserAccountAsync();
            if (!hasAccount)
            {
                await Shell.Current.GoToAsync(RouteConstants.OnboardingWelcome);
                return;
            }

            IsBiometricAvailable = await _securityService.IsBiometricAvailableAsync();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("LoginViewModel.Appearing", ex);
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;

        if (!ValidationHelper.IsValidPin(Pin))
        {
            ErrorMessage = "Please enter a valid PIN.";
            return;
        }

        IsBusy = true;
        try
        {
            var isValid = await _securityService.ValidatePinAsync(Pin);
            if (isValid)
            {
                App.TouchSession();
                await Shell.Current.GoToAsync($"//{RouteConstants.Dashboard}");
            }
            else
            {
                ErrorMessage = "Incorrect PIN. Please try again.";
                Pin = string.Empty;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ForgotPinAsync()
    {
        ErrorMessage = string.Empty;
        var question = await _securityService.GetSecurityQuestionAsync();
        if (string.IsNullOrEmpty(question))
        {
            ErrorMessage = "No security question set. Recovery is not available.";
            return;
        }

        SecurityQuestion = question;
        SecurityAnswer = string.Empty;
        NewPin = string.Empty;
        ConfirmNewPin = string.Empty;
        IsResettingPin = true;
    }

    [RelayCommand]
    private void CancelReset()
    {
        IsResettingPin = false;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ResetPinAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(SecurityAnswer))
        {
            ErrorMessage = "Please enter your security answer.";
            return;
        }

        var isValid = await _securityService.ValidateSecurityAnswerAsync(SecurityAnswer);
        if (!isValid)
        {
            ErrorMessage = "Incorrect answer. Please try again.";
            return;
        }

        if (!ValidationHelper.IsValidPin(NewPin))
        {
            ErrorMessage = $"New PIN must be {AppConstants.MinPinLength}-{AppConstants.MaxPinLength} digits.";
            return;
        }

        if (NewPin != ConfirmNewPin)
        {
            ErrorMessage = "PINs do not match.";
            return;
        }

        await _securityService.SetPinAsync(NewPin);
        IsResettingPin = false;
        ErrorMessage = string.Empty;
        Pin = string.Empty;
        await Shell.Current.DisplayAlert("Success", "Your PIN has been reset. Please log in with your new PIN.", "OK");
    }

    [RelayCommand]
    private async Task BiometricLoginAsync()
    {
        IsBusy = true;
        try
        {
            var authenticated = await _securityService.AuthenticateWithBiometricAsync("Authenticate to access your health data");
            if (authenticated)
            {
                App.TouchSession();
                await Shell.Current.GoToAsync($"//{RouteConstants.Dashboard}");
            }
            else
            {
                ErrorMessage = "Biometric authentication failed.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Authentication error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

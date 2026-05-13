using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

public partial class PersonalInfoViewModel : BaseViewModel
{
    private readonly IOnboardingCoordinator _coordinator;

    public PersonalInfoViewModel(IOnboardingCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        // Load existing data from coordinator
        _firstName = _coordinator.Data.FirstName;
        _lastName = _coordinator.Data.LastName;
        _selectedGender = _coordinator.Data.Gender;

        // Initialize date fields from stored DOB
        var dob = _coordinator.Data.DateOfBirth;
        if (dob > DateTime.MinValue && dob.Year > 1900)
        {
            _birthMonth = dob.Month.ToString();
            _birthDay = dob.Day.ToString();
            _birthYear = dob.Year.ToString();
        }
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    public List<Gender> GenderOptions => [Gender.Male, Gender.Female];

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private Gender _selectedGender;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    // Date of Birth as typed numeric fields
    [ObservableProperty]
    private string _birthMonth = string.Empty;

    [ObservableProperty]
    private string _birthDay = string.Empty;

    [ObservableProperty]
    private string _birthYear = string.Empty;

    [ObservableProperty]
    private string _dateOfBirthError = string.Empty;

    [ObservableProperty]
    private bool _hasDateOfBirthError;

    partial void OnFirstNameChanged(string value) => ValidateAndSync();
    partial void OnLastNameChanged(string value) => ValidateAndSync();
    partial void OnSelectedGenderChanged(Gender value) => ValidateAndSync();
    partial void OnBirthMonthChanged(string value) => ValidateAndSync();
    partial void OnBirthDayChanged(string value) => ValidateAndSync();
    partial void OnBirthYearChanged(string value) => ValidateAndSync();

    private bool TryParseDateOfBirth(out DateTime dob)
    {
        dob = default;

        if (string.IsNullOrWhiteSpace(BirthMonth) ||
            string.IsNullOrWhiteSpace(BirthDay) ||
            string.IsNullOrWhiteSpace(BirthYear))
            return false;

        if (!int.TryParse(BirthMonth, out var month) ||
            !int.TryParse(BirthDay, out var day) ||
            !int.TryParse(BirthYear, out var year))
            return false;

        if (month < 1 || month > 12 || day < 1 || day > 31 || year < 1900)
            return false;

        try
        {
            dob = new DateTime(year, month, day);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ValidateAndSync()
    {
        _coordinator.Data.FirstName = FirstName?.Trim() ?? string.Empty;
        _coordinator.Data.LastName = LastName?.Trim() ?? string.Empty;
        _coordinator.Data.Gender = SelectedGender;

        // Parse and validate DOB
        HasDateOfBirthError = false;
        DateOfBirthError = string.Empty;

        if (TryParseDateOfBirth(out var dob))
        {
            var minDate = DateTime.Today.AddYears(-100);
            var maxDate = DateTime.Today.AddYears(-13);

            if (dob < minDate || dob > maxDate)
            {
                HasDateOfBirthError = true;
                DateOfBirthError = "Age must be between 13 and 100 years.";
            }
            else
            {
                _coordinator.Data.DateOfBirth = dob;
            }
        }
        else if (!string.IsNullOrWhiteSpace(BirthYear) && BirthYear.Length == 4)
        {
            HasDateOfBirthError = true;
            DateOfBirthError = "Please enter a valid date.";
        }

        if (string.IsNullOrWhiteSpace(FirstName))
            ValidationMessage = "First name is required.";
        else if (string.IsNullOrWhiteSpace(LastName))
            ValidationMessage = "Last name is required.";
        else
            ValidationMessage = string.Empty;
    }

    private bool IsValid =>
        !string.IsNullOrWhiteSpace(FirstName) &&
        !string.IsNullOrWhiteSpace(LastName) &&
        TryParseDateOfBirth(out var dob) &&
        dob >= DateTime.Today.AddYears(-100) &&
        dob <= DateTime.Today.AddYears(-13) &&
        !HasDateOfBirthError;

    [RelayCommand]
    private async Task NextAsync()
    {
        ValidateAndSync();
        if (!IsValid)
        {
            if (string.IsNullOrWhiteSpace(BirthMonth) || string.IsNullOrWhiteSpace(BirthDay) || string.IsNullOrWhiteSpace(BirthYear))
            {
                HasDateOfBirthError = true;
                DateOfBirthError = "Date of birth is required.";
            }
            if (string.IsNullOrWhiteSpace(ValidationMessage))
                ValidationMessage = "Please fill in all required fields.";
            return;
        }
        await _coordinator.GoNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _coordinator.GoPreviousAsync();
    }
}

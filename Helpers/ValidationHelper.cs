using IntelligentPersonalHealthOptimization.Constants;

namespace IntelligentPersonalHealthOptimization.Helpers;

public static class ValidationHelper
{
    public static bool IsValidName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Trim().Length >= 2;
    }

    public static bool IsValidPin(string? pin)
    {
        if (string.IsNullOrEmpty(pin)) return false;
        return pin.Length >= AppConstants.MinPinLength
            && pin.Length <= AppConstants.MaxPinLength
            && pin.All(char.IsDigit);
    }

    public static bool IsValidWeight(double weight)
    {
        return weight > 20 && weight < 500;
    }

    public static bool IsValidHeight(double height)
    {
        return height > 50 && height < 300;
    }

    public static bool IsValidDateOfBirth(DateTime dob)
    {
        var age = DateTime.Today.Year - dob.Year;
        if (dob > DateTime.Today.AddYears(-age)) age--;
        return age >= 13 && age <= 120;
    }

    public static bool IsValidMeasurement(double value)
    {
        return value > 0 && value < 500;
    }
}

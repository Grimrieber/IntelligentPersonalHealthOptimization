namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface ISecurityService
{
    Task<string> GetOrCreateDatabaseKeyAsync();
    Task<bool> IsBiometricAvailableAsync();
    Task<bool> AuthenticateWithBiometricAsync(string reason);
    Task SetPinAsync(string pin);
    Task<bool> ValidatePinAsync(string pin);
    Task<bool> HasPinAsync();
    Task SetSecurityQuestionAsync(string question, string answer);
    Task<string?> GetSecurityQuestionAsync();
    Task<bool> ValidateSecurityAnswerAsync(string answer);
}

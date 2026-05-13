using System.Security.Cryptography;
using System.Text;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class SecurityService : ISecurityService
{
    public async Task<string> GetOrCreateDatabaseKeyAsync()
    {
        var existingKey = await SecureStorage.Default.GetAsync(AppConstants.DatabaseKeyStorageKey);
        if (!string.IsNullOrEmpty(existingKey))
            return existingKey;

        var keyBytes = RandomNumberGenerator.GetBytes(AppConstants.EncryptionKeyLengthBytes);
        var key = Convert.ToHexString(keyBytes);

        await SecureStorage.Default.SetAsync(AppConstants.DatabaseKeyStorageKey, key);
        return key;
    }

    public Task<bool> IsBiometricAvailableAsync()
    {
        // For MVP, biometric support is deferred - return false
        return Task.FromResult(false);
    }

    public Task<bool> AuthenticateWithBiometricAsync(string reason)
    {
        // For MVP, biometric auth is deferred
        return Task.FromResult(false);
    }

    public async Task SetPinAsync(string pin)
    {
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var hash = HashPin(pin, salt);

        await SecureStorage.Default.SetAsync(AppConstants.PinHashStorageKey, hash);
        await SecureStorage.Default.SetAsync(AppConstants.PinSaltStorageKey, salt);
    }

    public async Task<bool> ValidatePinAsync(string pin)
    {
        var storedHash = await SecureStorage.Default.GetAsync(AppConstants.PinHashStorageKey);
        var storedSalt = await SecureStorage.Default.GetAsync(AppConstants.PinSaltStorageKey);

        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            return false;

        var inputHash = HashPin(pin, storedSalt);
        return string.Equals(storedHash, inputHash, StringComparison.Ordinal);
    }

    public async Task<bool> HasPinAsync()
    {
        var storedHash = await SecureStorage.Default.GetAsync(AppConstants.PinHashStorageKey);
        return !string.IsNullOrEmpty(storedHash);
    }

    public async Task SetSecurityQuestionAsync(string question, string answer)
    {
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var hash = HashValue(answer.Trim().ToLowerInvariant(), salt);

        await SecureStorage.Default.SetAsync(AppConstants.SecurityQuestionStorageKey, question);
        await SecureStorage.Default.SetAsync(AppConstants.SecurityAnswerHashStorageKey, hash);
        await SecureStorage.Default.SetAsync(AppConstants.SecurityAnswerSaltStorageKey, salt);
    }

    public async Task<string?> GetSecurityQuestionAsync()
    {
        return await SecureStorage.Default.GetAsync(AppConstants.SecurityQuestionStorageKey);
    }

    public async Task<bool> ValidateSecurityAnswerAsync(string answer)
    {
        var storedHash = await SecureStorage.Default.GetAsync(AppConstants.SecurityAnswerHashStorageKey);
        var storedSalt = await SecureStorage.Default.GetAsync(AppConstants.SecurityAnswerSaltStorageKey);

        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            return false;

        var inputHash = HashValue(answer.Trim().ToLowerInvariant(), storedSalt);
        return string.Equals(storedHash, inputHash, StringComparison.Ordinal);
    }

    private static string HashValue(string value, string salt)
    {
        var combined = Encoding.UTF8.GetBytes(salt + value);
        var hash = SHA256.HashData(combined);
        return Convert.ToHexString(hash);
    }

    private static string HashPin(string pin, string salt)
    {
        var combined = Encoding.UTF8.GetBytes(salt + pin);
        var hash = SHA256.HashData(combined);
        return Convert.ToHexString(hash);
    }
}

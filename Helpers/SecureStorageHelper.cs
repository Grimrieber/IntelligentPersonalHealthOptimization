namespace IntelligentPersonalHealthOptimization.Helpers;

public static class SecureStorageHelper
{
    public static async Task<string?> GetAsync(string key)
    {
        return await SecureStorage.Default.GetAsync(key);
    }

    public static async Task SetAsync(string key, string value)
    {
        await SecureStorage.Default.SetAsync(key, value);
    }

    public static bool Remove(string key)
    {
        return SecureStorage.Default.Remove(key);
    }

    public static void RemoveAll()
    {
        SecureStorage.Default.RemoveAll();
    }
}

namespace MAUI_IOT.Services;

internal static class SessionStore
{
    public static async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch
        {
            return Preferences.Default.Get<string?>(key, null);
        }
    }

    public static async Task SetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            Preferences.Default.Set(key, value);
        }
    }

    public static void Remove(string key)
    {
        try
        {
            SecureStorage.Default.Remove(key);
        }
        catch
        {
        }

        Preferences.Default.Remove(key);
    }
}

namespace MAUI_IOT.Services;

public static class DeviceNameFormatter
{
    public static string ForDevice(string deviceId, string? storedName)
    {
        if (!string.IsNullOrWhiteSpace(storedName))
        {
            return storedName.Trim();
        }

        var shortId = deviceId.Length >= 8 ? deviceId[^8..].ToUpperInvariant() : deviceId;
        return $"DHT22 {shortId}";
    }
}

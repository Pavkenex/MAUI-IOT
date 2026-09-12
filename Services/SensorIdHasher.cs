namespace MAUI_IOT.Services;

public static class SensorIdHasher
{
    public static int ForDevice(string deviceId, bool isTemperature)
    {
        var key = $"{deviceId}|{(isTemperature ? "temp" : "hum")}";
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in key)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)hash;
        }
    }
}

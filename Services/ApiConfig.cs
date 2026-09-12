namespace MAUI_IOT.Services;

public static class ApiConfig
{
    // Base address of the Functions backend, including the /api route prefix.
    //   Deployed (current)                                      : https://mauiiot-api-pavke-fsagg3esfffkcme5.switzerlandnorth-01.azurewebsites.net/api/
    //   Physical phone via USB (adb reverse tcp:7071 tcp:7071) : http://localhost:7071/api/
    //   Android emulator                                        : http://10.0.2.2:7071/api/
    public const string BaseAddress = "https://mauiiot-api-pavke-fsagg3esfffkcme5.switzerlandnorth-01.azurewebsites.net/api/";
}

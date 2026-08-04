using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public interface IBluetoothScanService
{
    Task<bool> IsBluetoothEnabledAsync();
    Task EnableBluetoothAsync();
    Task<ScanSession> RunMockScanAsync();
    Task<IReadOnlyList<ScanSession>> GetScanSessionsAsync();
    Task<IReadOnlyList<EspBroadcastReading>> GetBroadcastsAsync(Guid sessionId);
}

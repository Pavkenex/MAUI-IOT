using MAUI_IOT.Models;

namespace MAUI_IOT.Services;

public interface IEspReadingRepository
{
    Task<bool> ExistsAsync(string deviceId, uint bootSessionId, uint readingId);

    Task<int> InsertIdempotentAsync(EspReading reading);

    Task<int> ImportCloudReadingsAsync(IReadOnlyList<EspReading> readings);

    Task<IReadOnlyList<EspReading>> GetReadingsAsync(string? deviceId = null, uint? bootSessionId = null, int limit = 200);

    Task<EspReading?> GetLatestReadingAsync(string deviceId);

    Task<IReadOnlyList<EspReading>> GetLastLocatedReadingsPerDeviceAsync(int maxDevices);

    Task<IReadOnlyList<EspReading>> GetLatestReadingsPerDeviceAsync(int maxDevices);

    Task SaveDeviceAsync(EspDeviceRecord device);

    Task<IReadOnlyList<EspDeviceRecord>> GetDevicesAsync();

    Task<IReadOnlyList<EspReading>> GetPendingUploadsAsync(int limit = 200);

    Task MarkUploadedAsync(IReadOnlyList<EspReading> readings);

    Task<EspSyncCursor?> GetCursorAsync(string deviceId);

    Task SaveCursorAsync(EspSyncCursor cursor);
}

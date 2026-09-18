using MAUI_IOT.Models;
using Microsoft.Maui.Storage;
using SQLite;

namespace MAUI_IOT.Services;

public sealed class EspReadingRepository : IEspReadingRepository
{
    private const string DatabaseFileName = "esp_readings.db3";
    private readonly SQLiteAsyncConnection _connection;

    public EspReadingRepository()
    {
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
        _connection = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    private async Task EnsureCreatedAsync()
    {
        await _connection.CreateTablesAsync<EspReading, EspSyncCursor, EspDeviceRecord>();
        await MigrateAsync();
    }

    private async Task MigrateAsync()
    {
        var columns = await _connection.QueryAsync<TableColumnInfo>("SELECT name FROM pragma_table_info('esp_readings')");
        var columnSet = new HashSet<string>(columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

        if (!columnSet.Contains("Latitude"))
        {
            await _connection.ExecuteAsync("ALTER TABLE esp_readings ADD COLUMN Latitude REAL");
        }

        if (!columnSet.Contains("Longitude"))
        {
            await _connection.ExecuteAsync("ALTER TABLE esp_readings ADD COLUMN Longitude REAL");
        }
    }

    private sealed class TableColumnInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    public async Task<bool> ExistsAsync(string deviceId, uint bootSessionId, uint readingId)
    {
        await EnsureCreatedAsync();
        return await _connection.Table<EspReading>()
            .Where(r => r.DeviceId == deviceId && r.BootSessionId == bootSessionId && r.ReadingId == readingId)
            .CountAsync() > 0;
    }

    public async Task<int> InsertIdempotentAsync(EspReading reading)
    {
        await EnsureCreatedAsync();
        if (await ExistsAsync(reading.DeviceId, reading.BootSessionId, reading.ReadingId))
        {
            return 0;
        }

        try
        {
            return await _connection.InsertAsync(reading);
        }
        catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
        {
            return 0;
        }
    }

    public async Task<int> ImportCloudReadingsAsync(IReadOnlyList<EspReading> readings)
    {
        await EnsureCreatedAsync();

        var imported = 0;
        foreach (var reading in readings)
        {
            if (await ExistsAsync(reading.DeviceId, reading.BootSessionId, reading.ReadingId))
            {
                continue;
            }

            reading.IsUploaded = true;
            reading.UploadedAtUtc ??= DateTimeOffset.UtcNow;

            try
            {
                await _connection.InsertAsync(reading);
                imported++;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
            }
        }

        foreach (var device in readings
            .Where(r => !string.IsNullOrWhiteSpace(r.DeviceName))
            .GroupBy(r => r.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new EspDeviceRecord
            {
                DeviceId = g.Key,
                Name = g.First().DeviceName!,
                LastSeenAtUtc = DateTimeOffset.UtcNow,
            }))
        {
            await SaveDeviceAsync(device);
        }

        return imported;
    }

    public async Task<IReadOnlyList<EspReading>> GetReadingsAsync(
        string? deviceId = null,
        uint? bootSessionId = null,
        int limit = 200)
    {
        await EnsureCreatedAsync();
        var query = _connection.Table<EspReading>();
        if (deviceId is not null)
        {
            query = query.Where(r => r.DeviceId == deviceId);
        }
        if (bootSessionId is not null)
        {
            query = query.Where(r => r.BootSessionId == bootSessionId);
        }

        return await query
            .OrderByDescending(r => r.RecordedAtUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<EspReading?> GetLatestReadingAsync(string deviceId)
    {
        await EnsureCreatedAsync();
        return await _connection.Table<EspReading>()
            .Where(r => r.DeviceId == deviceId)
            .OrderByDescending(r => r.RecordedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<EspReading>> GetLastLocatedReadingsPerDeviceAsync(int maxDevices)
    {
        await EnsureCreatedAsync();
        var readings = await _connection.Table<EspReading>()
            .OrderByDescending(r => r.RecordedAtUtc)
            .ToListAsync();

        return readings
            .Where(r => r.Latitude is not null && r.Longitude is not null)
            .GroupBy(r => r.DeviceId)
            .Select(group => group.First())
            .OrderByDescending(r => r.RecordedAtUtc)
            .Take(maxDevices)
            .ToList();
    }

    public async Task<IReadOnlyList<EspReading>> GetLatestReadingsPerDeviceAsync(int maxDevices)
    {
        await EnsureCreatedAsync();
        var readings = await _connection.Table<EspReading>()
            .OrderByDescending(r => r.RecordedAtUtc)
            .ToListAsync();

        return readings
            .GroupBy(r => r.DeviceId)
            .Select(group => group.First())
            .OrderByDescending(r => r.RecordedAtUtc)
            .Take(maxDevices)
            .ToList();
    }

    public async Task SaveDeviceAsync(EspDeviceRecord device)
    {
        await EnsureCreatedAsync();
        var existing = await _connection.Table<EspDeviceRecord>()
            .Where(d => d.DeviceId == device.DeviceId)
            .FirstOrDefaultAsync();
        if (existing is null)
        {
            await _connection.InsertAsync(device);
        }
        else
        {
            existing.Name = device.Name;
            existing.LastSeenAtUtc = device.LastSeenAtUtc;
            await _connection.UpdateAsync(existing);
        }
    }

    public async Task<IReadOnlyList<EspDeviceRecord>> GetDevicesAsync()
    {
        await EnsureCreatedAsync();
        return await _connection.Table<EspDeviceRecord>().ToListAsync();
    }

    public async Task<IReadOnlyList<EspReading>> GetPendingUploadsAsync(int limit = 200)
    {
        await EnsureCreatedAsync();
        return await _connection.Table<EspReading>()
            .Where(r => !r.IsUploaded)
            .OrderBy(r => r.RecordedAtUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task MarkUploadedAsync(IReadOnlyList<EspReading> readings)
    {
        await EnsureCreatedAsync();
        foreach (var reading in readings)
        {
            reading.IsUploaded = true;
            reading.UploadedAtUtc = DateTimeOffset.UtcNow;
        }

        await _connection.UpdateAllAsync(readings);
    }

    public async Task<EspSyncCursor?> GetCursorAsync(string deviceId)
    {
        await EnsureCreatedAsync();
        return await _connection.Table<EspSyncCursor>()
            .Where(c => c.DeviceId == deviceId)
            .FirstOrDefaultAsync();
    }

    public async Task SaveCursorAsync(EspSyncCursor cursor)
    {
        await EnsureCreatedAsync();
        var existing = await GetCursorAsync(cursor.DeviceId);
        if (existing is null)
        {
            await _connection.InsertAsync(cursor);
        }
        else
        {
            await _connection.UpdateAsync(cursor);
        }
    }
}

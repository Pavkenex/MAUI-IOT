namespace MAUI_IOT.Models;

public sealed class AuthResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string Username { get; set; } = string.Empty;
}

public sealed class ApiErrorResponse
{
    public string? Error { get; set; }
}

public sealed class ReadingUploadRequest
{
    public List<ReadingUploadItem> Readings { get; set; } = [];
}

public sealed class ReadingUploadItem
{
    public string DeviceId { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public uint BootSessionId { get; set; }

    public uint ReadingId { get; set; }

    public uint ElapsedSeconds { get; set; }

    public double TemperatureCelsius { get; set; }

    public double HumidityPercent { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }
}

public sealed class ReadingUploadResponse
{
    public int Accepted { get; set; }
}

public sealed class ReadingsResponse
{
    public List<ReadingListItem> Readings { get; set; } = [];
}

public sealed class ReadingListItem
{
    public string Id { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public uint BootSessionId { get; set; }

    public uint ReadingId { get; set; }

    public uint ElapsedSeconds { get; set; }

    public double TemperatureCelsius { get; set; }

    public double HumidityPercent { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}

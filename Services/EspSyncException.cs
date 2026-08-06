namespace MAUI_IOT.Services;

public sealed class EspSyncException : Exception
{
    public EspSyncException(string message)
        : base(message)
    {
    }

    public EspSyncException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

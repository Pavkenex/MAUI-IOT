namespace MAUI_IOT.Protocol;

public static class EspProtocol
{
    public const byte Version = 1;

    public static readonly Guid ServiceUuid = new("7b1e0001-6a2b-4f19-8f61-4d54484d3230");
    public static readonly Guid IdentityUuid = new("7b1e0002-6a2b-4f19-8f61-4d54484d3230");
    public static readonly Guid UptimeUuid = new("7b1e0003-6a2b-4f19-8f61-4d54484d3230");
    public static readonly Guid RangeUuid = new("7b1e0004-6a2b-4f19-8f61-4d54484d3230");
    public static readonly Guid ControlUuid = new("7b1e0005-6a2b-4f19-8f61-4d54484d3230");
    public static readonly Guid DataUuid = new("7b1e0006-6a2b-4f19-8f61-4d54484d3230");
    public static readonly Guid DeviceInfoUuid = new("7b1e0007-6a2b-4f19-8f61-4d54484d3230");

    public const int IdentityLength = 15;
    public const int UptimeLength = 4;
    public const int RangeLength = 14;
    public const int ControlPacketLength = 10;
    public const int MetaPacketLength = 16;
    public const int ValuesPacketLength = 18;
    public const int EndPacketLength = 18;
    public const int ErrorPacketLength = 16;
    public const int DeviceInfoPacketLength = 20;
    public const int DeviceInfoTextOffset = 2;
    public const int DeviceInfoTextCapacity = 18;

    public enum ControlCommand : byte
    {
        RequestAfter = 0x01,
        Ack = 0x02,
        Cancel = 0x03,
    }

    public enum DataPacketType : byte
    {
        DeviceInfo = 0x00,
        ReadingMeta = 0x01,
        ReadingValues = 0x02,
        TransferEnd = 0x03,
        Error = 0x7F,
    }

    public enum EspErrorCode : byte
    {
        InvalidCommand = 0x01,
        SessionMismatch = 0x02,
        ReadingOverwritten = 0x03,
    }

    public static string FormatDeviceId(ReadOnlySpan<byte> deviceId)
    {
        return string.Join(":", deviceId.ToArray().Select(b => b.ToString("X2")));
    }
}

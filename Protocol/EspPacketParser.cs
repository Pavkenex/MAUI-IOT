using System.Buffers.Binary;

namespace MAUI_IOT.Protocol;

public static class EspPacketParser
{
    public static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset));
    }

    public static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
    }

    public static short ReadInt16(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset));
    }

    public static EspDeviceIdentity ParseIdentity(ReadOnlySpan<byte> data)
    {
        if (data.Length != EspProtocol.IdentityLength)
        {
            throw new EspProtocolException(
                $"Identity packet has invalid length {data.Length}; expected {EspProtocol.IdentityLength}.");
        }
        if (data[0] != EspProtocol.Version)
        {
            throw new EspProtocolException($"Unsupported protocol version {data[0]}.");
        }

        return new EspDeviceIdentity(
            data[0],
            EspProtocol.FormatDeviceId(data.Slice(1, 6)),
            ReadUInt32(data, 7),
            ReadUInt16(data, 11),
            ReadUInt16(data, 13));
    }

    public static uint ParseUptime(ReadOnlySpan<byte> data)
    {
        if (data.Length != EspProtocol.UptimeLength)
        {
            throw new EspProtocolException(
                $"Uptime packet has invalid length {data.Length}; expected {EspProtocol.UptimeLength}.");
        }

        return ReadUInt32(data, 0);
    }

    public static EspReadingRange ParseRange(ReadOnlySpan<byte> data)
    {
        if (data.Length != EspProtocol.RangeLength)
        {
            throw new EspProtocolException(
                $"Range packet has invalid length {data.Length}; expected {EspProtocol.RangeLength}.");
        }

        return new EspReadingRange(
            ReadUInt32(data, 0),
            ReadUInt32(data, 4),
            ReadUInt32(data, 8),
            ReadUInt16(data, 12));
    }

    public static EspReadingMeta ParseMeta(ReadOnlySpan<byte> data)
    {
        if (data.Length != EspProtocol.MetaPacketLength)
        {
            throw new EspProtocolException(
                $"META packet has invalid length {data.Length}; expected {EspProtocol.MetaPacketLength}.");
        }
        if (data[0] != (byte)EspProtocol.DataPacketType.ReadingMeta)
        {
            throw new EspProtocolException($"Not a META packet (type 0x{data[0]:X2}).");
        }
        if (data[1] != EspProtocol.Version)
        {
            throw new EspProtocolException($"Unsupported protocol version {data[1]}.");
        }

        return new EspReadingMeta(
            data[1],
            EspProtocol.FormatDeviceId(data.Slice(2, 6)),
            ReadUInt32(data, 8),
            ReadUInt32(data, 12));
    }

    public static EspReadingValues ParseValues(ReadOnlySpan<byte> data)
    {
        if (data.Length != EspProtocol.ValuesPacketLength)
        {
            throw new EspProtocolException(
                $"VALUES packet has invalid length {data.Length}; expected {EspProtocol.ValuesPacketLength}.");
        }
        if (data[0] != (byte)EspProtocol.DataPacketType.ReadingValues)
        {
            throw new EspProtocolException($"Not a VALUES packet (type 0x{data[0]:X2}).");
        }
        if (data[1] != EspProtocol.Version)
        {
            throw new EspProtocolException($"Unsupported protocol version {data[1]}.");
        }

        var rawTemperature = ReadInt16(data, 14);
        var rawHumidity = ReadUInt16(data, 16);

        return new EspReadingValues(
            data[1],
            ReadUInt32(data, 2),
            ReadUInt32(data, 6),
            ReadUInt32(data, 10),
            rawTemperature / 100.0,
            rawHumidity / 100.0);
    }

    public static EspTransferEnd ParseTransferEnd(ReadOnlySpan<byte> data)
    {
        if (data.Length != EspProtocol.EndPacketLength)
        {
            throw new EspProtocolException(
                $"TRANSFER_END packet has invalid length {data.Length}; expected {EspProtocol.EndPacketLength}.");
        }
        if (data[0] != (byte)EspProtocol.DataPacketType.TransferEnd)
        {
            throw new EspProtocolException($"Not a TRANSFER_END packet (type 0x{data[0]:X2}).");
        }
        if (data[1] != EspProtocol.Version)
        {
            throw new EspProtocolException($"Unsupported protocol version {data[1]}.");
        }

        return new EspTransferEnd(
            data[1],
            ReadUInt32(data, 2),
            ReadUInt32(data, 6),
            ReadUInt32(data, 10),
            ReadUInt32(data, 14));
    }

    public static EspErrorInfo ParseError(ReadOnlySpan<byte> data)
    {
        if (data.Length != EspProtocol.ErrorPacketLength)
        {
            throw new EspProtocolException(
                $"ERROR packet has invalid length {data.Length}; expected {EspProtocol.ErrorPacketLength}.");
        }
        if (data[0] != (byte)EspProtocol.DataPacketType.Error)
        {
            throw new EspProtocolException($"Not an ERROR packet (type 0x{data[0]:X2}).");
        }
        if (data[1] != EspProtocol.Version)
        {
            throw new EspProtocolException($"Unsupported protocol version {data[1]}.");
        }

        return new EspErrorInfo(
            data[1],
            (EspProtocol.EspErrorCode)data[2],
            ReadUInt32(data, 4),
            ReadUInt32(data, 8),
            ReadUInt32(data, 12));
    }
}

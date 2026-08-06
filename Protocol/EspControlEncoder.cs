using System.Buffers.Binary;

namespace MAUI_IOT.Protocol;

public static class EspControlEncoder
{
    public static byte[] RequestAfter(uint bootSessionId, uint readingId)
    {
        return BuildCommand(EspProtocol.ControlCommand.RequestAfter, bootSessionId, readingId);
    }

    public static byte[] Ack(uint bootSessionId, uint readingId)
    {
        return BuildCommand(EspProtocol.ControlCommand.Ack, bootSessionId, readingId);
    }

    public static byte[] Cancel()
    {
        return [EspProtocol.ControlCommand.Cancel, EspProtocol.Version];
    }

    public static byte[] BuildCommand(EspProtocol.ControlCommand command, uint bootSessionId, uint readingId)
    {
        var packet = new byte[EspProtocol.ControlPacketLength];
        packet[0] = (byte)command;
        packet[1] = EspProtocol.Version;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(2), bootSessionId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(6), readingId);
        return packet;
    }
}

using System.IO.Ports;

namespace Hase.Transport.Serial;

/// <summary>
/// Maps transport-independent serial settings to System.IO.Ports values.
/// </summary>
internal static class SystemIoPortsSerialSettingsMapper
{
    public static Parity MapParity(
        SerialParity parity)
    {
        return parity switch
        {
            SerialParity.None =>
                Parity.None,

            SerialParity.Odd =>
                Parity.Odd,

            SerialParity.Even =>
                Parity.Even,

            SerialParity.Mark =>
                Parity.Mark,

            SerialParity.Space =>
                Parity.Space,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(parity),
                    parity,
                    "The serial parity value is not supported.")
        };
    }

    public static StopBits MapStopBits(
        SerialStopBits stopBits)
    {
        return stopBits switch
        {
            SerialStopBits.One =>
                StopBits.One,

            SerialStopBits.OnePointFive =>
                StopBits.OnePointFive,

            SerialStopBits.Two =>
                StopBits.Two,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(stopBits),
                    stopBits,
                    "The serial stop-bit value is not supported.")
        };
    }

    public static Handshake MapHandshake(
        SerialHandshake handshake)
    {
        return handshake switch
        {
            SerialHandshake.None =>
                Handshake.None,

            SerialHandshake.XOnXOff =>
                Handshake.XOnXOff,

            SerialHandshake.RequestToSend =>
                Handshake.RequestToSend,

            SerialHandshake.RequestToSendXOnXOff =>
                Handshake.RequestToSendXOnXOff,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(handshake),
                    handshake,
                    "The serial handshake value is not supported.")
        };
    }
}
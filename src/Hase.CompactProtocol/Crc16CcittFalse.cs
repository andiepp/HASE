namespace Hase.CompactProtocol;

/// <summary>
/// Calculates CRC-16/CCITT-FALSE values.
/// </summary>
internal static class Crc16CcittFalse
{
    private const ushort InitialValue =
        0xFFFF;

    private const ushort Polynomial =
        0x1021;

    public static ushort Calculate(
        ReadOnlySpan<byte> data)
    {
        ushort crc =
            InitialValue;

        foreach (byte value in data)
        {
            crc ^=
                (ushort)(value << 8);

            for (int bit = 0; bit < 8; bit++)
            {
                crc =
                    (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ Polynomial)
                        : (ushort)(crc << 1);
            }
        }

        return crc;
    }
}
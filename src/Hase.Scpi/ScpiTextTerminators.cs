namespace Hase.Scpi;

internal static class ScpiTextTerminators
{
    public static byte[] GetBytes(ScpiCommandTerminator terminator) => terminator switch
    {
        ScpiCommandTerminator.CarriageReturn => [0x0D],
        ScpiCommandTerminator.LineFeed => [0x0A],
        ScpiCommandTerminator.CarriageReturnLineFeed => [0x0D, 0x0A],
        _ => throw new ArgumentOutOfRangeException(nameof(terminator))
    };

    public static byte[] GetBytes(ScpiResponseTerminator terminator) => terminator switch
    {
        ScpiResponseTerminator.CarriageReturn => [0x0D],
        ScpiResponseTerminator.LineFeed => [0x0A],
        ScpiResponseTerminator.CarriageReturnLineFeed => [0x0D, 0x0A],
        _ => throw new ArgumentOutOfRangeException(nameof(terminator))
    };
}

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal enum Kel103CommandTerminator
{
    CarriageReturn = 0,
    LineFeed = 1,
    CarriageReturnLineFeed = 2
}

internal static class Kel103CommandTerminatorExtensions
{
    public static string ToArgumentValue(
        this Kel103CommandTerminator terminator)
    {
        return terminator switch
        {
            Kel103CommandTerminator.CarriageReturn =>
                "cr",
            Kel103CommandTerminator.LineFeed =>
                "lf",
            Kel103CommandTerminator.CarriageReturnLineFeed =>
                "crlf",
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(terminator),
                    terminator,
                    "The KEL-103 command terminator is not supported.")
        };
    }

    public static byte[] ToBytes(
        this Kel103CommandTerminator terminator)
    {
        return terminator switch
        {
            Kel103CommandTerminator.CarriageReturn =>
                [0x0D],
            Kel103CommandTerminator.LineFeed =>
                [0x0A],
            Kel103CommandTerminator.CarriageReturnLineFeed =>
                [0x0D, 0x0A],
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(terminator),
                    terminator,
                    "The KEL-103 command terminator is not supported.")
        };
    }
}


using System.Globalization;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class Kel103ReadOnlyCharacterizationScenario
    : IParameterizedScenario
{
    private const int DefaultBaudRate =
        115200;

    private readonly ISerialByteStreamFactory _byteStreamFactory;
    private readonly Func<
        Kel103CommandTerminator,
        Kel103CharacterizationOptions> _optionsFactory;

    public Kel103ReadOnlyCharacterizationScenario()
        : this(
            new SystemIoPortsSerialByteStreamFactory(),
            commandTerminator =>
                new Kel103CharacterizationOptions(
                    commandTerminator))
    {
    }

    internal Kel103ReadOnlyCharacterizationScenario(
        ISerialByteStreamFactory byteStreamFactory,
        Func<
            Kel103CommandTerminator,
            Kel103CharacterizationOptions> optionsFactory)
    {
        _byteStreamFactory =
            byteStreamFactory
            ?? throw new ArgumentNullException(
                nameof(byteStreamFactory));

        _optionsFactory =
            optionsFactory
            ?? throw new ArgumentNullException(
                nameof(optionsFactory));
    }

    public string Name =>
        "kel103-characterize";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        ExecuteAsync(
                arguments)
            .GetAwaiter()
            .GetResult();
    }

    private async Task ExecuteAsync(
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count is < 2 or > 3)
        {
            throw new ArgumentException(
                "KEL-103 characterization requires a COM port and one explicit terminator token (cr, lf, or crlf), and accepts an optional baud rate.",
                nameof(arguments));
        }

        string portName =
            arguments[0];

        Kel103CommandTerminator commandTerminator =
            ParseCommandTerminator(
                arguments[1]);

        int baudRate =
            arguments.Count == 3
                ? ParseBaudRate(
                    arguments[2])
                : DefaultBaudRate;

        var transportOptions =
            new SerialTransportOptions(
                portName,
                baudRate);

        Kel103CharacterizationOptions characterizationOptions =
            _optionsFactory(
                commandTerminator);

        WriteHeader(
            transportOptions,
            characterizationOptions);

        var characterizer =
            new Kel103ReadOnlySerialCharacterizer(
                _byteStreamFactory);

        Kel103CharacterizationResult result =
            await characterizer.CharacterizeAsync(
                transportOptions,
                characterizationOptions);

        WriteResult(
            result);
    }

    internal static Kel103CommandTerminator ParseCommandTerminator(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        return value.ToLowerInvariant() switch
        {
            "cr" =>
                Kel103CommandTerminator.CarriageReturn,
            "lf" =>
                Kel103CommandTerminator.LineFeed,
            "crlf" =>
                Kel103CommandTerminator.CarriageReturnLineFeed,
            _ =>
                throw new ArgumentException(
                    $"'{value}' is not a supported KEL-103 terminator token. Use cr, lf, or crlf.",
                    nameof(value))
        };
    }

    internal static int ParseBaudRate(
        string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int baudRate)
            || baudRate != DefaultBaudRate)
        {
            throw new ArgumentException(
                $"'{value}' is not the characterized KEL-103 baud rate. Use {DefaultBaudRate}.",
                nameof(value));
        }

        return baudRate;
    }

    internal static void WriteHeader(
        SerialTransportOptions transportOptions,
        Kel103CharacterizationOptions characterizationOptions)
    {
        const string title =
            "KEL-103 Read-Only Serial Characterization";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Send exactly one fixed read-only *IDN? query to a KEL-103 electronic load.");

        Console.WriteLine();

        Console.WriteLine(
            "Serial target          : External runtime argument");

        Console.WriteLine(
            $"Baud rate             : {transportOptions.BaudRate}");

        Console.WriteLine(
            $"Serial framing        : {transportOptions.DataBits} data, {transportOptions.Parity} parity, {transportOptions.StopBits} stop");

        Console.WriteLine(
            $"Flow control          : {transportOptions.Handshake}");

        Console.WriteLine(
            $"Command terminator    : {characterizationOptions.CommandTerminator.ToArgumentValue()}");

        Console.WriteLine(
            $"Total timeout         : {characterizationOptions.TotalResponseTimeout.TotalMilliseconds:F0} ms");

        Console.WriteLine(
            "Response terminator   : lf");

        Console.WriteLine(
            $"Maximum response      : {characterizationOptions.MaximumResponseBytes} bytes");

        Console.WriteLine();

        Console.WriteLine(
            "Opening the external serial target and sending one read-only query.");

        Console.WriteLine();
    }

    private static void WriteResult(
        Kel103CharacterizationResult result)
    {
        Console.WriteLine(
            "Read-only characterization succeeded.");

        Console.WriteLine();

        Console.WriteLine(
            $"Response bytes        : {result.ResponseByteCount}");

        Console.WriteLine(
            $"Response terminator   : {result.ResponseTerminator}");

        Console.WriteLine(
            $"Command echo detected : {result.CommandEchoDetected}");

        Console.WriteLine(
            $"Time to first byte    : {result.TimeToFirstByte.TotalMilliseconds:F1} ms");

        Console.WriteLine(
            $"Total duration        : {result.TotalDuration.TotalMilliseconds:F1} ms");

        Console.WriteLine(
            $"Product identity      : {result.ProductIdentity}");

        Console.WriteLine(
            $"Firmware              : {result.Firmware}");

        Console.WriteLine(
            "Instrument serial     : <redacted>");

        Console.WriteLine(
            $"Verification          : {(result.IdentityVerified ? "Succeeded" : "Failed")}");

        Console.WriteLine();

        Console.WriteLine(
            "The serial connection has been closed. No state-changing command was sent.");
    }
}
